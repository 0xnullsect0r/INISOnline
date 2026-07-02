using Inis.Core.Moves;

namespace Inis.Core.Model;

/// <summary>The game moment that opened a reaction (Triskel) window.</summary>
public enum ReactionTrigger
{
    /// <summary>An Action card was played and has not resolved yet (Geis may cancel it).</summary>
    ActionCardPlayed,
    /// <summary>A card was cancelled by Geis (Lug Samildanach may keep it).</summary>
    GeisCancelled,
    /// <summary>An Epic Tale resolved and is about to be discarded (Master Craftsman).</summary>
    EpicTalePlayed,
    /// <summary>A clash just started, before its Citadels step (Warlord may join).</summary>
    ClashStarted,
    /// <summary>An Attack maneuver fully resolved (Bard / Raid).</summary>
    AttackResolved,
    /// <summary>A card effect awaits a decision from another player (e.g. Coalition's partner).</summary>
    CardFollowUp,
}

/// <summary>What the engine resumes once a reaction window closes.</summary>
public enum ReactionContinuation
{
    /// <summary>Resolve (or, if cancelled, just discard) the interrupted card play.</summary>
    ResolvePlayedCard,
    /// <summary>Discard the played Epic Tale, then continue the turn.</summary>
    DiscardPlayedCard,
    /// <summary>Continue the clash with its Citadels step.</summary>
    BeginCitadelStep,
    /// <summary>Continue the clash after a completed maneuver.</summary>
    AfterManeuver,
    /// <summary>Continue the Season turn rotation (unless a clash is active).</summary>
    ResumeSeasonTurn,
    /// <summary>Coalition: both movers are in; check the destination for a clash, then continue.</summary>
    CoalitionClash,
}

/// <summary>
/// One open reaction (Triskel) window. Frames live on <see cref="GameState.ReactionStack"/> so
/// an interrupted game serializes, reloads, and resumes mid-window deterministically. The queue
/// is fixed when the window opens (eligible holders in turn order); the cursor advances as
/// players pass. Everything in a frame is public information — being prompted necessarily
/// reveals that a player holds a matching Triskel card (the standard digital-adaptation
/// compromise, noted in docs/protocol.md).
/// </summary>
public sealed class ReactionFrame
{
    public required ReactionTrigger Trigger { get; init; }

    /// <summary>Player ids eligible to react, in turn order.</summary>
    public List<string> Queue { get; init; } = new();

    /// <summary>Index of the reactor currently being prompted.</summary>
    public int Cursor { get; set; }

    /// <summary>The player whose action opened the window (card player / attacker / instigator).</summary>
    public string? TriggerPlayerId { get; init; }

    /// <summary>The card whose play opened the window, if any.</summary>
    public string? TriggerCardId { get; init; }

    public string? TerritoryId { get; init; }

    /// <summary>Second territory for two-territory follow-ups (e.g. Coalition's destination).</summary>
    public string? SecondaryTerritoryId { get; init; }

    /// <summary>Secondary subject (e.g. the attacked player for Raid).</summary>
    public string? TargetPlayerId { get; init; }

    /// <summary>The interrupted PlayCard intent, kept so resolution can resume after the window.</summary>
    public Move? TriggerMove { get; init; }

    public required ReactionContinuation Continuation { get; init; }

    /// <summary>Set when Geis cancelled the interrupted card.</summary>
    public bool Cancelled { get; set; }

    /// <summary>True when the triggering attack removed at least one clan (enables Bard).</summary>
    public bool ClansRemoved { get; init; }
}
