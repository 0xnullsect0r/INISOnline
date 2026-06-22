using System.Collections.Generic;
using System.Linq;
using Inis.Core.Ai;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

namespace INISOnline.Game;

/// <summary>
/// Drives an offline / hotseat game on the embedded <c>Inis.Core</c> engine — the same engine
/// the server runs authoritatively. The UI reads <see cref="LegalMoves"/>, applies the chosen
/// <see cref="Move"/>, and lets AI seats auto-play. This is exactly the engine contract used
/// online, so the client validates the rules through real play (per docs/plan.md Phase 3).
/// </summary>
public sealed class LocalGame
{
    private readonly GameEngine _engine;
    private readonly HashSet<string> _aiSeats;
    private readonly GameData _data = GameData.Default;

    public IReadOnlyList<SeatConfig> Seats { get; }
    public List<string> Log { get; } = new();

    public LocalGame(int seed, IReadOnlyList<SeatConfig> seats)
    {
        Seats = seats;
        _engine = GameEngine.Create("local", seed, seats);
        _aiSeats = seats.Where(s => s.IsAi).Select(s => s.PlayerId).ToHashSet();
    }

    public GameState State => _engine.State;
    public PendingDecision? Pending => _engine.Pending;
    public bool IsGameOver => _engine.State.Phase == GamePhase.GameOver;
    public bool IsAiTurn => Pending is { } p && _aiSeats.Contains(p.PlayerId);

    public string SeatName(string playerId) =>
        Seats.FirstOrDefault(s => s.PlayerId == playerId)?.DisplayName ?? playerId;

    public IReadOnlyList<Move> LegalMoves() => _engine.LegalMoves();

    public void Apply(Move move) => Record(_engine.Apply(move));

    /// <summary>Plays one AI move (call only when <see cref="IsAiTurn"/>).</summary>
    public void StepAi() => Record(_engine.Apply(HeuristicAi.ChooseMove(_engine)));

    /// <summary>A short human-readable label for a legal move (for menu buttons).</summary>
    public string Describe(Move move) => move.Type switch
    {
        MoveType.DraftPick => $"Draft: {CardName(move.CardId)}",
        MoveType.PlayCard => $"Play: {CardName(move.CardId)}",
        MoveType.Pass => "Pass",
        MoveType.TakePretender => "Claim victory (take a Pretender)",
        MoveType.Attack => $"Attack {SeatName(move.TargetPlayerId ?? "")}",
        MoveType.Withdraw => "Withdraw",
        MoveType.EndClash => "Offer to end the clash",
        MoveType.ClashShelter => "Shelter a clan in a Citadel",
        MoveType.ClashSkipShelter => "Do not shelter",
        MoveType.AttackRemoveClan => "Lose a clan",
        MoveType.AttackDiscardCard => "Discard an Action card",
        MoveType.Resign => "Resign",
        _ => move.Type.ToString(),
    };

    private string CardName(string? cardId) =>
        cardId is not null && _data.TryGetCard(cardId, out var def) ? def.Name : cardId ?? "?";

    private void Record(IReadOnlyList<GameEvent> events)
    {
        foreach (var e in events)
        {
            var who = e.PlayerId is not null ? SeatName(e.PlayerId) : null;
            var card = e.CardId is not null && _data.TryGetCard(e.CardId, out var def) ? def.Name : e.CardId;
            var parts = new[] { who, e.Kind, card, e.Detail }.Where(s => !string.IsNullOrEmpty(s));
            Log.Add(string.Join(" · ", parts));
        }
        if (Log.Count > 200) Log.RemoveRange(0, Log.Count - 200);
    }
}
