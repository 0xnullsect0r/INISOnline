namespace Inis.Core.Model;

/// <summary>
/// Per-game configuration chosen at creation (and persisted with the state). Currently the
/// expansion toggle: the base 2016 game vs. <em>Seasons of Inis</em>, which adds its action cards,
/// a 5th clan / 5th seat, and (later) the season board, harbours and sea travel.
/// </summary>
public sealed record GameOptions(bool SeasonsOfInis = false)
{
    public static readonly GameOptions Base = new(false);
    public static readonly GameOptions Seasons = new(true);

    /// <summary>Max seats: 4 for the base game, 5 with Seasons of Inis (the 5th clan).</summary>
    public int MaxPlayers => SeasonsOfInis ? 5 : 4;

    public string Label => SeasonsOfInis ? "Seasons of Inis" : "Base game";
}
