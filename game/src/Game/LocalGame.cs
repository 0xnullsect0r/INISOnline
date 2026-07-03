using System.Collections.Generic;
using System.Linq;
using Inis.Core.Ai;
using Inis.Core.Data;
using Inis.Core.Debug;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

namespace INISOnline.Game;

/// <summary>
/// Drives an offline / hotseat game on the embedded <c>Inis.Core</c> engine — the same engine
/// the server runs authoritatively. The UI reads <see cref="LegalMoves"/>, submits the chosen
/// <see cref="Move"/>, and AI seats auto-play (paced in <see cref="Poll"/>). This is exactly the
/// engine contract used online, so the client validates the rules through real play.
/// </summary>
public sealed class LocalGame : IGameSource
{
    private const double AiStepSeconds = 0.25;

    private readonly GameEngine _engine;
    private readonly HashSet<string> _aiSeats;
    private readonly GameData _data = GameData.Default;
    private double _aiClock;

    public IReadOnlyList<SeatConfig> Seats { get; }
    public List<string> LogLines { get; } = new();

    public LocalGame(int seed, IReadOnlyList<SeatConfig> seats, GameOptions? options = null)
    {
        Seats = seats;
        _engine = GameEngine.Create("local", seed, seats, options: options);
        _aiSeats = seats.Where(s => s.IsAi).Select(s => s.PlayerId).ToHashSet();
    }

    public bool Ready => true;
    public GameState State => _engine.State;
    public PendingDecision? Pending => _engine.Pending;
    public bool IsGameOver => _engine.State.Phase == GamePhase.GameOver;
    public bool IsAiTurn => Pending is { } p && _aiSeats.Contains(p.PlayerId);
    public bool CanLocalAct => !IsGameOver && !IsAiTurn;
    public IReadOnlyList<string> Log => LogLines;

    public string StatusLine => IsGameOver
        ? $"Winner: {SeatName(State.WinnerId ?? "?")}"
        : IsAiTurn ? "AI is thinking…"
        : Pending is { } p ? $"{SeatName(p.PlayerId)} — your move" : "—";

    public IReadOnlyList<Move> LegalMoves() => _engine.LegalMoves();

    public string SeatName(string playerId) =>
        Seats.FirstOrDefault(s => s.PlayerId == playerId)?.DisplayName ?? playerId;

    public string TerritoryName(string instanceId) =>
        State.Territories.TryGetValue(instanceId, out var t) ? _data.Territory(t.DefinitionId).Name : instanceId;

    public string Describe(Move move) => MoveText.Describe(move, SeatName, _data);

    public void Submit(Move move) => Record(_engine.Apply(move));

    // Hotseat: the local player is whichever human must act right now.
    public string? LocalPlayerId => CanLocalAct ? Pending?.PlayerId : null;
    public bool SupportsChat => false;
    public void SendChat(string text) { /* everyone shares a couch offline */ }

    public void Debug(string command, string? cardId, int amount)
    {
        var pid = Pending?.PlayerId;
        if (pid is null) return;
        var move = new Move { Type = MoveType.Debug, PlayerId = pid, DebugCommand = command, CardId = cardId, Amount = amount };
        Record(DebugCommandApi.Apply(_engine, move));
    }

    /// <summary>Paces AI seats: steps one AI move per <see cref="AiStepSeconds"/>.</summary>
    public bool Poll(double delta)
    {
        if (IsGameOver || !IsAiTurn) return false;
        _aiClock += delta;
        if (_aiClock < AiStepSeconds) return false;
        _aiClock = 0;
        Record(_engine.Apply(HeuristicAi.ChooseMove(_engine)));
        return true;
    }

    private void Record(IReadOnlyList<GameEvent> events)
    {
        foreach (var e in events) LogLines.Add(MoveText.DescribeEvent(e, SeatName, _data));
        if (LogLines.Count > 200) LogLines.RemoveRange(0, LogLines.Count - 200);
    }
}
