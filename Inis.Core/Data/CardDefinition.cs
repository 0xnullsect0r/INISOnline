using Inis.Core.Model;

namespace Inis.Core.Data;

/// <summary>
/// Static, data-driven definition of a single card type, loaded from the embedded
/// JSON (<c>Data/cards.json</c>). The <see cref="EffectId"/> maps to a handler in
/// the effect registry; runtime behaviour lives in code, wording/quantities here.
/// </summary>
public sealed record CardDefinition
{
    /// <summary>Stable identifier, e.g. "action.clash", "epic.king_and_land".</summary>
    public required string Id { get; init; }

    public required CardType Type { get; init; }

    /// <summary>Display name, e.g. "Clash", "The King and the Land".</summary>
    public required string Name { get; init; }

    /// <summary>Rules text shown to the player (re-worded, not Matagot's verbatim copy).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>How many physical copies exist in the deck (drives deck construction).</summary>
    public int Count { get; init; } = 1;

    /// <summary>Identifier the effect registry uses to resolve this card. Defaults to <see cref="Id"/>.</summary>
    public string? EffectId { get; init; }

    /// <summary>For Advantage cards: the territory id this advantage is attached to.</summary>
    public string? TerritoryId { get; init; }

    /// <summary>Relative path (under assets) to the card's art, if any.</summary>
    public string? Art { get; init; }

    /// <summary>
    /// True once the name/text/count have been confirmed against the official
    /// rulebook. False entries are provisional and tracked in docs/rules.md.
    /// </summary>
    public bool Verified { get; init; }

    public string ResolvedEffectId => EffectId ?? Id;
}
