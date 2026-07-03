using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using INISOnline.Game;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;

namespace INISOnline.Net;

/// <summary>
/// Shared <see cref="IGameSource"/> for any WebSocket-driven game (online or LAN). It owns the
/// redacted state received from the host and the legal moves from a TurnPrompt, applies incoming
/// messages on the main thread via <see cref="Poll"/>, and submits the chosen move as an Intent.
/// Subclasses provide the transport: enqueue raw frames with <see cref="EnqueueIncoming"/>, send
/// with <see cref="SendRaw"/>, and optionally pump a polled socket in <see cref="PumpTransport"/>.
/// </summary>
public abstract class WsGameSourceBase : IGameSource
{
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly List<string> _log = new();
    private IReadOnlyList<Move> _legal = Array.Empty<Move>();

    protected readonly GameData Data = GameData.Default;
    protected GameState? CurrentState;
    protected string? MyPlayerId;
    protected bool IsSpectator;
    protected string ConnectStatus = "Connecting…";

    protected void EnqueueIncoming(string json) => _incoming.Enqueue(json);
    protected abstract void SendRaw(string json);

    /// <summary>Advance a polled transport (e.g. a Godot WebSocketPeer). No-op for async sockets.</summary>
    protected virtual void PumpTransport() { }

    // ---- IGameSource ----

    public bool Ready => CurrentState is not null;
    public GameState State => CurrentState ?? throw new InvalidOperationException("State not yet received.");
    public PendingDecision? Pending => CurrentState?.Pending;
    public bool IsGameOver => CurrentState?.Phase == GamePhase.GameOver;

    public bool CanLocalAct =>
        Ready && !IsSpectator && !IsGameOver &&
        Pending is { } p && p.PlayerId == MyPlayerId && _legal.Count > 0;

    public string StatusLine
    {
        get
        {
            if (!Ready) return ConnectStatus;
            if (IsGameOver) return $"Winner: {SeatName(State.WinnerId ?? "?")}";
            if (IsSpectator) return "Spectating";
            if (Pending is { } p)
                return p.PlayerId == MyPlayerId ? "Your move" : $"Waiting for {SeatName(p.PlayerId)}…";
            return "—";
        }
    }

    public IReadOnlyList<Move> LegalMoves() => _legal;
    public IReadOnlyList<string> Log => _log;

    public string SeatName(string playerId) =>
        CurrentState?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.DisplayName ?? playerId;

    public string TerritoryName(string instanceId) =>
        CurrentState is not null && CurrentState.Territories.TryGetValue(instanceId, out var t)
            ? Data.Territory(t.DefinitionId).Name : instanceId;

    public string Describe(Move move) => MoveText.Describe(move, SeatName, Data);

    public void Submit(Move move)
    {
        SendRaw(JsonSerializer.Serialize(new
        {
            v = Protocol.Version,
            type = Protocol.Intent,
            payload = move,
        }, InisJson.Options));
        _legal = Array.Empty<Move>(); // wait for the host's authoritative response
    }

    public string? LocalPlayerId => IsSpectator ? null : MyPlayerId;
    public bool SupportsChat => true;

    public void SendChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SendRaw(JsonSerializer.Serialize(new
        {
            v = Protocol.Version,
            type = Protocol.Chat,
            payload = new { text = text.Trim() },
        }, InisJson.Options));
    }

    public void Debug(string command, string? cardId, int amount) =>
        SendRaw(JsonSerializer.Serialize(new
        {
            v = Protocol.Version,
            type = Protocol.DebugCommand,
            payload = new { command, cardId, amount },
        }, InisJson.Options));

    public bool Poll(double delta)
    {
        PumpTransport();
        var changed = false;
        while (_incoming.TryDequeue(out var json)) changed |= Handle(json);
        return changed;
    }

    private bool Handle(string json)
    {
        var env = Envelope.TryParse(json);
        if (env is null) return false;
        if (env.V != Protocol.Version)
        {
            AppendLog($"⚠ Host speaks protocol v{env.V}, this client v{Protocol.Version} — please update.");
            return true;
        }
        switch (env.Type)
        {
            case Protocol.Hello:
                var hello = env.PayloadAs<HelloPayload>();
                MyPlayerId = hello?.PlayerId;
                IsSpectator = hello?.Spectator ?? false;
                return true;
            case Protocol.StateSync:
                CurrentState = env.PayloadAs<GameState>();
                return true;
            case Protocol.TurnPrompt:
                _legal = (IReadOnlyList<Move>?)env.PayloadAs<TurnPromptPayload>()?.LegalMoves ?? Array.Empty<Move>();
                return true;
            case Protocol.Event:
                var ev = env.PayloadAs<GameEvent>();
                if (ev is not null) AppendLog(MoveText.DescribeEvent(ev, SeatName, Data));
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

    protected void AppendLog(string line)
    {
        _log.Add(line);
        if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
    }

    private sealed record HelloPayload(string GameId, string PlayerId, bool Spectator);
    private sealed record TurnPromptPayload(string PlayerId, List<Move> LegalMoves);
    private sealed record ChatPayload(string FromPlayerId, string Text);
    private sealed record ErrorPayload(string Code, string Message);
}
