using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using INISOnline.Game;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;

namespace INISOnline.Net;

/// <summary>
/// Drives the HUD from an authoritative server game over a WebSocket. It reuses the shared
/// <c>Inis.Core.Net</c> wire contract: it receives per-player redacted <c>StateSync</c>,
/// <c>TurnPrompt</c> and <c>Event</c> messages and submits the chosen legal <see cref="Move"/>
/// back as an <c>Intent</c>. The receive loop runs on a background task and only enqueues raw
/// messages; <see cref="Poll"/> applies them on Godot's main thread. Drops auto-reconnect (the
/// server replays a fresh StateSync on connect).
/// </summary>
public sealed class RemoteGame : IGameSource, IDisposable
{
    private readonly Uri _uri;
    private readonly GameData _data = GameData.Default;
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    private ClientWebSocket? _ws;
    private GameState? _state;
    private string? _myPlayerId;
    private bool _spectator;
    private IReadOnlyList<Move> _legal = Array.Empty<Move>();
    private readonly List<string> _log = new();
    private string _status = "Connecting…";

    public RemoteGame(string gameId)
    {
        _uri = new Uri($"{Session.WebSocketBase}/ws/game/{gameId}?access_token={Session.AccessToken}");
        _ = Task.Run(ReceiveLoopAsync);
    }

    public bool Ready => _state is not null;
    public GameState State => _state ?? throw new InvalidOperationException("State not yet received.");
    public PendingDecision? Pending => _state?.Pending;
    public bool IsGameOver => _state?.Phase == GamePhase.GameOver;

    public bool CanLocalAct =>
        Ready && !_spectator && !IsGameOver &&
        Pending is { } p && p.PlayerId == _myPlayerId && _legal.Count > 0;

    public string StatusLine
    {
        get
        {
            if (!Ready) return _status;
            if (IsGameOver) return $"Winner: {SeatName(State.WinnerId ?? "?")}";
            if (_spectator) return "Spectating";
            if (Pending is { } p)
                return p.PlayerId == _myPlayerId ? "Your move" : $"Waiting for {SeatName(p.PlayerId)}…";
            return "—";
        }
    }

    public IReadOnlyList<Move> LegalMoves() => _legal;
    public IReadOnlyList<string> Log => _log;

    public string SeatName(string playerId) =>
        _state?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.DisplayName ?? playerId;

    public string TerritoryName(string instanceId) =>
        _state is not null && _state.Territories.TryGetValue(instanceId, out var t)
            ? _data.Territory(t.DefinitionId).Name : instanceId;

    public string Describe(Move move) => MoveText.Describe(move, SeatName, _data);

    public void Submit(Move move)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            v = Protocol.Version,
            type = Protocol.Intent,
            payload = move,
        }, InisJson.Options);
        _ = SendAsync(envelope);
        // Clear local legal moves until the server's authoritative response arrives.
        _legal = Array.Empty<Move>();
    }

    /// <summary>Drains queued server messages on the main thread; returns true if anything changed.</summary>
    public bool Poll(double delta)
    {
        var changed = false;
        while (_incoming.TryDequeue(out var json))
            changed |= Handle(json);
        return changed;
    }

    private bool Handle(string json)
    {
        var env = Envelope.TryParse(json);
        if (env is null) return false;
        switch (env.Type)
        {
            case Protocol.Hello:
                var hello = env.PayloadAs<HelloPayload>();
                _myPlayerId = hello?.PlayerId;
                _spectator = hello?.Spectator ?? false;
                return true;
            case Protocol.StateSync:
                _state = env.PayloadAs<GameState>();
                return true;
            case Protocol.TurnPrompt:
                _legal = (IReadOnlyList<Move>?)env.PayloadAs<TurnPromptPayload>()?.LegalMoves ?? Array.Empty<Move>();
                return true;
            case Protocol.Event:
                var ev = env.PayloadAs<GameEvent>();
                if (ev is not null) AppendLog(MoveText.DescribeEvent(ev, SeatName, _data));
                return true;
            case Protocol.Chat:
                var chat = env.PayloadAs<ChatPayload>();
                if (chat is not null) AppendLog($"{SeatName(chat.FromPlayerId)}: {chat.Text}");
                return true;
            case Protocol.Error:
                var err = env.PayloadAs<ErrorPayload>();
                if (err is not null) AppendLog($"⚠ {err.Message}");
                return true;
            default:
                return false;
        }
    }

    private void AppendLog(string line)
    {
        _log.Add(line);
        if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
    }

    // ------------------------------------------------------------- transport

    private async Task ReceiveLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(_uri, _cts.Token);
                _status = "Connected";
                var buffer = new byte[32 * 1024];
                using var ms = new MemoryStream();
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;
                    _incoming.Enqueue(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { _status = "Reconnecting…"; }

            if (_cts.IsCancellationRequested) return;
            // The server replays a full StateSync on reconnect; back off briefly and retry.
            try { await Task.Delay(1000, _cts.Token); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task SendAsync(string text)
    {
        if (_ws is not { State: WebSocketState.Open }) return;
        await _sendGate.WaitAsync(_cts.Token);
        try { await _ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, _cts.Token); }
        catch (Exception) { /* surfaced via reconnect */ }
        finally { _sendGate.Release(); }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _ws?.Dispose(); } catch { /* ignore */ }
        _cts.Dispose();
    }

    // Payload shapes for the host→client messages.
    private sealed record HelloPayload(string GameId, string PlayerId, bool Spectator);
    private sealed record TurnPromptPayload(string PlayerId, List<Move> LegalMoves);
    private sealed record ChatPayload(string FromPlayerId, string Text);
    private sealed record ErrorPayload(string Code, string Message);
}
