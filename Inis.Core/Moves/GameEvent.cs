namespace Inis.Core.Moves;

/// <summary>
/// An animatable fact emitted by the engine after applying a move (card played, clash hit,
/// building placed, deed gained, …). The networking layer maps these to protocol Events.
/// </summary>
public sealed record GameEvent(string Kind, string? PlayerId = null, string? CardId = null,
    string? TerritoryId = null, string? Detail = null);
