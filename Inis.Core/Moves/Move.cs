using Inis.Core.Model;

namespace Inis.Core.Moves;

/// <summary>
/// A single intent submitted to the engine. One record covers every move type; only the
/// fields relevant to <see cref="Type"/> are read. Legal moves are produced by the
/// engine's <c>LegalMoves</c> method and consumed by its <c>Apply</c> method.
/// </summary>
public sealed record Move
{
    public required MoveType Type { get; init; }

    /// <summary>The acting player. Defaults to the pending player when omitted by callers.</summary>
    public string? PlayerId { get; init; }

    /// <summary>Card definition id for DraftPick / PlayCard / AttackDiscardCard / Debug grants.</summary>
    public string? CardId { get; init; }

    /// <summary>Primary target territory (instance id).</summary>
    public string? TerritoryId { get; init; }

    /// <summary>Source / destination territory (instance ids) for moves.</summary>
    public string? FromTerritoryId { get; init; }
    public string? ToTerritoryId { get; init; }

    /// <summary>Targeted opponent (e.g. Attack target, New Alliance victim).</summary>
    public string? TargetPlayerId { get; init; }

    /// <summary>A targeted clan color (e.g. which color to remove).</summary>
    public ClanColor? TargetColor { get; init; }

    /// <summary>A generic amount (clans to place/move, etc.).</summary>
    public int Amount { get; init; }

    /// <summary>Card ids chosen for multi-card effects (Druid pick, Master Craftsman discard…).</summary>
    public IReadOnlyList<string>? CardIds { get; init; }

    /// <summary>Debug command verb (see <c>Inis.Core.Debug</c>).</summary>
    public string? DebugCommand { get; init; }

    public static Move Pass(string? player = null) => new() { Type = MoveType.Pass, PlayerId = player };
}
