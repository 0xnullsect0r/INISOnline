namespace Inis.Core.Model;

/// <summary>What the engine is currently waiting for: which player must act, and how.</summary>
public sealed class PendingDecision
{
    public required PendingKind Kind { get; init; }
    public required string PlayerId { get; init; }

    /// <summary>Optional context — e.g. the card mid-resolution awaiting a follow-up choice.</summary>
    public string? CardId { get; init; }
}
