using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Inis.Core.Ai;
using Inis.Core.Data;
using Inis.Core.Debug;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;
using Inis.Core.Rules;

namespace INISOnline.Lan;

/// <summary>
/// The client-hosted authoritative LAN session. It serves the <em>same</em> WebSocket protocol as
/// the online server (<c>Inis.Core.Net</c>): peers connect, claim a seat with <c>Join</c>, then
/// stream intents and receive per-player redacted StateSync/Event/TurnPrompt. The host's own
/// player plays in-process via <see cref="LanHostGame"/>. Rules live only in <c>Inis.Core</c>; this
/// is just transport + orchestration (the engine, redaction and intent mapping are all shared).
/// Polled on the main thread as a Node.
/// </summary>
public partial class LanHost : Node
{
    private static readonly ClanColor[] SeatColors =
        { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };

    private sealed class Seat
    {
        public int Index;
        public ClanColor Color;       // assigned at Start
        public string? Name;          // occupant display name (human), else null
        public bool LocalHost;        // played in-process by this app's host UI
        public Conn? Conn;            // remote peer connection, if any
        public bool IsAi;             // decided at Start for empty seats
        public string PlayerId = "";  // assigned at Start
    }

    private sealed class Conn
    {
        public required WebSocketPeer Ws;
        public int Seat = -1;
        public bool Joined;
        public bool Spectator;
        public string PlayerId => Seat >= 0 ? $"lan-{Seat}" : "spectator";
    }

    private readonly TcpServer _server = new();
    private readonly List<Conn> _conns = new();
    private readonly GameData _data = GameData.Default;
    private Seat[] _seats = System.Array.Empty<Seat>();
    private GameEngine? _engine;

    public int Capacity { get; private set; }
    public int Port { get; private set; }
    public bool Started { get; private set; }

    /// <summary>Bumped on every state change so in-process views (the host) can refresh.</summary>
    public int Version { get; private set; }

    public GameEngine Engine => _engine!;
    public string? LocalHostPlayerId { get; private set; }

    /// <summary>Starts the host on an OS-assigned port. Returns false if the socket fails.</summary>
    public bool Open(int capacity)
    {
        Capacity = Mathf.Clamp(capacity, 2, 5);
        _seats = Enumerable.Range(0, Capacity).Select(i => new Seat { Index = i }).ToArray();

        var err = _server.Listen(0); // 0 → any free port
        if (err != Error.Ok) return false;
        Port = _server.GetLocalPort();
        return true;
    }

    /// <summary>Reserves the next open seat for the in-process host player; returns its player id.</summary>
    public string ClaimLocalSeat(string name)
    {
        var seat = _seats.First(s => s.Name is null && s.Conn is null);
        seat.Name = name;
        seat.LocalHost = true;
        LocalHostPlayerId = $"lan-{seat.Index}";
        Version++;
        return LocalHostPlayerId;
    }

    /// <summary>Seat occupancy for the host's lobby UI: name, or null if open.</summary>
    public IReadOnlyList<string?> SeatNames => _seats.Select(s => s.Name).ToList();

    public override void _Process(double delta)
    {
        AcceptConnections();
        PumpConnections();
    }

    private void AcceptConnections()
    {
        while (_server.IsConnectionAvailable())
        {
            var tcp = _server.TakeConnection();
            var ws = new WebSocketPeer();
            if (ws.AcceptStream(tcp) == Error.Ok)
                _conns.Add(new Conn { Ws = ws });
        }
    }

    private void PumpConnections()
    {
        foreach (var conn in _conns.ToList())
        {
            conn.Ws.Poll();
            switch (conn.Ws.GetReadyState())
            {
                case WebSocketPeer.State.Open:
                    while (conn.Ws.GetAvailablePacketCount() > 0)
                        HandleFrame(conn, Encoding.UTF8.GetString(conn.Ws.GetPacket()));
                    break;
                case WebSocketPeer.State.Closed:
                    DropConnection(conn);
                    break;
            }
        }
    }

    private void DropConnection(Conn conn)
    {
        _conns.Remove(conn);
        if (conn.Seat >= 0 && _seats[conn.Seat].Conn == conn)
            _seats[conn.Seat].Conn = null; // its turns are auto-played until it returns
        Version++;
    }

    private void HandleFrame(Conn conn, string json)
    {
        var env = Envelope.TryParse(json);
        if (env is null) return;

        if (env.Type == Protocol.Join)
        {
            AssignSeat(conn, env.PayloadAs<JoinPayload>()?.Name ?? "Player");
            return;
        }
        if (!Started || conn.Spectator || conn.Seat < 0) return;

        // Intents only: map to a Move attributed to this connection's seat and apply.
        var move = SafeToMove(env, conn.PlayerId);
        if (move is null) return;
        if (move.Type == MoveType.Debug)
        {
            ApplyEvents(DebugCommandApi.Apply(_engine!, move));
            return;
        }
        if (_engine!.Pending?.PlayerId != conn.PlayerId)
        {
            SendTo(conn, ServerMessages.Error("not_your_turn", "It is not your turn."));
            return;
        }
        TryApply(conn, move);
    }

    private void AssignSeat(Conn conn, string name)
    {
        if (Started)
        {
            // Reconnect: take over a seat matching the name whose peer has dropped.
            var match = _seats.FirstOrDefault(s => !s.IsAi && !s.LocalHost && s.Name == name && s.Conn is null);
            if (match is not null) { conn.Seat = match.Index; match.Conn = conn; }
            else conn.Spectator = true;
        }
        else
        {
            var open = _seats.FirstOrDefault(s => s.Name is null && s.Conn is null);
            if (open is not null) { conn.Seat = open.Index; open.Conn = conn; open.Name = name; }
            else conn.Spectator = true;
        }
        conn.Joined = true;
        Version++;
        SendTo(conn, ServerMessages.Hello("lan", conn.PlayerId, conn.Spectator));
        if (Started) SendSnapshot(conn);
    }

    /// <summary>Builds the engine: occupied seats become humans, empty ones AI, then begins play.</summary>
    public void Start()
    {
        if (Started) return;
        var configs = new List<SeatConfig>(Capacity);
        for (var i = 0; i < Capacity; i++)
        {
            var seat = _seats[i];
            seat.Color = SeatColors[i];
            var occupied = seat.LocalHost || seat.Conn is not null;
            seat.IsAi = !occupied;
            seat.PlayerId = seat.IsAi ? $"ai-{i}" : $"lan-{i}";
            var name = seat.IsAi ? $"AI {i + 1}" : seat.Name ?? $"Player {i + 1}";
            configs.Add(new SeatConfig(seat.PlayerId, name, seat.Color, seat.IsAi));
        }

        var seed = (int)(Time.GetUnixTimeFromSystem() % int.MaxValue);
        _engine = GameEngine.Create("lan", seed, configs);
        Started = true;
        DriveAi();
        BroadcastAll(System.Array.Empty<GameEvent>());
        Version++;
    }

    // ---- host-side play (called by LanHostGame) ----

    public IReadOnlyList<Move> HostLegalMoves() => _engine?.LegalMoves() ?? new List<Move>();

    public void HostApply(Move move)
    {
        if (_engine!.Pending?.PlayerId != LocalHostPlayerId) return;
        ApplyAndAdvance(move);
    }

    private void TryApply(Conn conn, Move move)
    {
        try { ApplyAndAdvance(move); }
        catch (System.InvalidOperationException ex)
        {
            SendTo(conn, ServerMessages.Error("illegal_move", ex.Message));
        }
    }

    private void ApplyAndAdvance(Move move)
    {
        var events = new List<GameEvent>(_engine!.Apply(move));
        events.AddRange(DriveAi());
        BroadcastAll(events);
        Version++;
    }

    private void ApplyEvents(IReadOnlyList<GameEvent> events)
    {
        var all = new List<GameEvent>(events);
        all.AddRange(DriveAi());
        BroadcastAll(all);
        Version++;
    }

    /// <summary>Auto-plays AI seats and the turns of any human seat whose peer has dropped.</summary>
    private List<GameEvent> DriveAi()
    {
        var events = new List<GameEvent>();
        var guard = 0;
        while (_engine!.State.Phase != GamePhase.GameOver && _engine.Pending is { } p
               && IsAutoSeat(p.PlayerId) && guard++ < 10_000)
            events.AddRange(_engine.Apply(HeuristicAi.ChooseMove(_engine)));
        return events;
    }

    private bool IsAutoSeat(string playerId)
    {
        var seat = _seats.FirstOrDefault(s => s.PlayerId == playerId);
        if (seat is null) return false;
        if (seat.IsAi) return true;
        if (seat.LocalHost) return false;           // the host plays its own seat
        return seat.Conn is null;                    // dropped peer → auto-play
    }

    private void BroadcastAll(IReadOnlyList<GameEvent> events)
    {
        foreach (var conn in _conns.Where(c => c.Ws.GetReadyState() == WebSocketPeer.State.Open))
        {
            SendTo(conn, ServerMessages.StateSync(_engine!.State, conn.Spectator ? null : conn.PlayerId));
            foreach (var e in events) SendTo(conn, ServerMessages.Event(e));
            if (_engine.Pending is { } p && p.PlayerId == conn.PlayerId)
                SendTo(conn, ServerMessages.TurnPrompt(p.PlayerId, _engine.LegalMoves()));
        }
    }

    private void SendSnapshot(Conn conn)
    {
        SendTo(conn, ServerMessages.StateSync(_engine!.State, conn.Spectator ? null : conn.PlayerId));
        if (_engine.Pending is { } p && p.PlayerId == conn.PlayerId)
            SendTo(conn, ServerMessages.TurnPrompt(p.PlayerId, _engine.LegalMoves()));
    }

    private static void SendTo(Conn conn, string message) => conn.Ws.SendText(message);

    private Move? SafeToMove(Envelope env, string playerId)
    {
        try { return MoveCodec.ToMove(env, playerId); }
        catch { return null; }
    }

    public void Shutdown()
    {
        foreach (var conn in _conns) conn.Ws.Close();
        _conns.Clear();
        _server.Stop();
    }

    private sealed record JoinPayload(string Name);
}
