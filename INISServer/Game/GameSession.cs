using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Inis.Core.Ai;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;
using Inis.Core.Rules;
using Inis.Core.Debug;
using InisServer.Data;
using Microsoft.EntityFrameworkCore;

namespace InisServer.Game;

/// <summary>
/// An authoritative, in-memory session for one game. Holds the single <see cref="GameEngine"/>,
/// the connected sockets, and the seat→player mapping. It is the only writer of its engine:
/// all mutation goes through <see cref="_gate"/>, AI seats are auto-played to the next human
/// decision, and every change is persisted and broadcast as <em>per-player redacted</em> state.
/// See docs/protocol.md.
/// </summary>
public sealed class GameSession
{
    private readonly GameEngine _engine;
    private readonly IReadOnlyList<SeatInfo> _seats;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, Connection> _connections = new();

    public Guid GameId { get; }

    /// <summary>Last time a connection was opened or a message handled (for idle eviction).</summary>
    public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

    public int ConnectionCount => _connections.Count;

    public bool IsFinished => _engine.State.Phase == GamePhase.GameOver;

    public GameSession(Guid gameId, GameEngine engine, IReadOnlyList<SeatInfo> seats,
        IServiceScopeFactory scopes, ILogger log)
    {
        GameId = gameId;
        _engine = engine;
        _seats = seats;
        _scopes = scopes;
        _log = log;
    }

    private sealed class Connection
    {
        public required Guid ConnId { get; init; }
        public required Guid UserId { get; init; }
        public required string PlayerId { get; init; } // seat player id, or a spectator tag
        public required WebSocket Socket { get; init; }
        public bool IsSpectator { get; init; }
    }

    /// <summary>The seat player id for a user, or null if they hold no seat (a spectator).</summary>
    public string? PlayerIdForUser(Guid userId) =>
        _seats.FirstOrDefault(s => s.UserId == userId)?.PlayerId;

    private bool IsAiSeat(string playerId) => _seats.Any(s => s.PlayerId == playerId && s.IsAi);

    // ----------------------------------------------------------------- lifecycle

    /// <summary>Drives any leading AI seats (e.g. an AI Brenn) and persists before anyone connects.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            DriveAi(new List<GameEvent>());
            await PersistAsync(ct);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Accepts a connected socket, replays state, and runs its receive loop until close.</summary>
    public async Task RunConnectionAsync(WebSocket socket, Guid userId, CancellationToken ct)
    {
        var playerId = PlayerIdForUser(userId);
        var spectator = playerId is null;
        var conn = new Connection
        {
            ConnId = Guid.NewGuid(),
            UserId = userId,
            PlayerId = playerId ?? $"spectator:{userId}",
            Socket = socket,
            IsSpectator = spectator,
        };
        _connections[conn.ConnId] = conn;
        LastActivityUtc = DateTimeOffset.UtcNow;
        try
        {
            // Reconnection / first connect: replay a full redacted StateSync + the pending prompt.
            await SendAsync(conn, ServerMessages.Hello(GameId.ToString(), conn.PlayerId, spectator), ct);
            await SendSnapshotAsync(conn, ct);

            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var (text, closed) = await ReceiveTextAsync(socket, buffer, ct);
                if (closed) break;
                if (text is not null) await HandleInboundAsync(conn, text, ct);
            }
        }
        catch (OperationCanceledException) { /* server shutting down */ }
        catch (WebSocketException) { /* client dropped */ }
        finally
        {
            _connections.TryRemove(conn.ConnId, out _);
        }
    }

    // ------------------------------------------------------------------- intents

    private async Task HandleInboundAsync(Connection conn, string json, CancellationToken ct)
    {
        LastActivityUtc = DateTimeOffset.UtcNow;
        var env = Envelope.TryParse(json);
        if (env is null) { await SendAsync(conn, ServerMessages.Error("bad_envelope", "Malformed message."), ct); return; }

        if (env.Type == Protocol.Chat)
        {
            var text = env.PayloadAs<ChatPayload>()?.Text;
            if (!string.IsNullOrWhiteSpace(text))
                await BroadcastAsync(new[] { ServerMessages.Chat(conn.PlayerId, text!.Trim()) }, ct);
            return;
        }

        if (conn.IsSpectator)
        {
            await SendAsync(conn, ServerMessages.Error("spectator", "Spectators cannot act."), ct);
            return;
        }

        Move? move;
        try { move = MoveCodec.ToMove(env, conn.PlayerId); }
        catch (Exception ex) { await SendAsync(conn, ServerMessages.Error("bad_intent", ex.Message), ct); return; }
        if (move is null) { await SendAsync(conn, ServerMessages.Error("unknown_type", $"Unhandled type '{env.Type}'."), ct); return; }

        List<(Connection conn, string msg)> outbound;
        await _gate.WaitAsync(ct);
        try
        {
            List<GameEvent> events;
            try
            {
                events = ApplyAuthoritative(move);
            }
            catch (InvalidOperationException ex)
            {
                await SendAsync(conn, ServerMessages.Error("illegal_move", ex.Message), ct);
                return;
            }
            await PersistAsync(ct);
            outbound = BuildBroadcast(events);
        }
        finally { _gate.Release(); }

        await FlushAsync(outbound, ct);
    }

    /// <summary>Applies a move (or debug command) and then auto-plays AI seats. Caller holds the gate.</summary>
    private List<GameEvent> ApplyAuthoritative(Move move)
    {
        var events = new List<GameEvent>();

        if (move.Type == MoveType.Debug)
        {
            // Debug/cheat commands mutate canonical state and are broadcast like any move
            // (works in real online games, per the product decision). Server-logged for audit.
            _log.LogInformation("DebugCommand {Command} by {Player} in game {Game}",
                move.DebugCommand, move.PlayerId, GameId);
            events.AddRange(DebugCommandApi.Apply(_engine, move));
        }
        else
        {
            // Only the seat the engine is waiting on may submit a normal move.
            var pending = _engine.Pending;
            if (pending is null || pending.PlayerId != move.PlayerId)
                throw new InvalidOperationException("It is not your turn.");
            events.AddRange(_engine.Apply(move));
        }

        DriveAi(events);
        return events;
    }

    /// <summary>Auto-plays AI seats until a human must decide or the game ends. Caller holds the gate.</summary>
    private void DriveAi(List<GameEvent> sink)
    {
        var guard = 0;
        while (_engine.State.Phase != GamePhase.GameOver
               && _engine.Pending is { } p && IsAiSeat(p.PlayerId)
               && guard++ < 10_000)
        {
            sink.AddRange(_engine.Apply(HeuristicAi.ChooseMove(_engine)));
        }
    }

    // ---------------------------------------------------------------- broadcast

    private List<(Connection, string)> BuildBroadcast(IReadOnlyList<GameEvent> events)
    {
        var outbound = new List<(Connection, string)>();
        var pending = _engine.Pending;
        foreach (var conn in _connections.Values)
        {
            var recipient = conn.IsSpectator ? null : conn.PlayerId;
            outbound.Add((conn, ServerMessages.StateSync(_engine.State, recipient)));
            foreach (var e in events) outbound.Add((conn, ServerMessages.Event(e)));
            if (pending is not null && pending.PlayerId == conn.PlayerId)
                outbound.Add((conn, ServerMessages.TurnPrompt(pending.PlayerId, _engine.LegalMoves())));
        }
        return outbound;
    }

    /// <summary>Sends the current redacted state + pending prompt to a single connection.</summary>
    private async Task SendSnapshotAsync(Connection conn, CancellationToken ct)
    {
        string sync, prompt;
        bool hasPrompt;
        await _gate.WaitAsync(ct);
        try
        {
            var recipient = conn.IsSpectator ? null : conn.PlayerId;
            sync = ServerMessages.StateSync(_engine.State, recipient);
            var pending = _engine.Pending;
            hasPrompt = pending is not null && pending.PlayerId == conn.PlayerId;
            prompt = hasPrompt ? ServerMessages.TurnPrompt(pending!.PlayerId, _engine.LegalMoves()) : "";
        }
        finally { _gate.Release(); }

        await SendAsync(conn, sync, ct);
        if (hasPrompt) await SendAsync(conn, prompt, ct);
    }

    private async Task FlushAsync(List<(Connection conn, string msg)> outbound, CancellationToken ct)
    {
        foreach (var (conn, msg) in outbound)
            await SendAsync(conn, msg, ct);
    }

    private async Task BroadcastAsync(IEnumerable<string> messages, CancellationToken ct)
    {
        foreach (var conn in _connections.Values)
            foreach (var msg in messages)
                await SendAsync(conn, msg, ct);
    }

    private async Task SendAsync(Connection conn, string message, CancellationToken ct)
    {
        if (conn.Socket.State != WebSocketState.Open) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await conn.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            _connections.TryRemove(conn.ConnId, out _);
        }
    }

    private static async Task<(string? text, bool closed)> ReceiveTextAsync(
        WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.CloseReceived)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                return (null, true);
            }
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return (Encoding.UTF8.GetString(ms.ToArray()), false);
    }

    // ---------------------------------------------------------------- persistence

    private async Task PersistAsync(CancellationToken ct)
    {
        var stateJson = InisJson.SerializeState(_engine.State);
        var status = _engine.State.Phase == GamePhase.GameOver ? GameStatus.Completed : GameStatus.Active;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Games.FirstOrDefaultAsync(g => g.Id == GameId, ct);
        if (row is null) return;
        row.StateJson = stateJson;
        row.Status = status;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private sealed record ChatPayload(string? Text);
}
