namespace Inis.Core.Model;

/// <summary>
/// Per-game configuration chosen at creation (and persisted with the state):
/// <list type="bullet">
/// <item><see cref="SeasonsOfInis"/> — base 2016 game vs. the expansion (its action cards, a 5th
/// clan/seat; the season board, harbours and sea travel come later).</item>
/// <item><see cref="Extended"/> — the house-ruled 6–8 player mode: extra clans, a doubled action
/// deck so the draft has enough cards, and a raised seat cap (non-official).</item>
/// </list>
/// </summary>
public sealed record GameOptions(bool SeasonsOfInis = false, bool Extended = false)
{
    public static readonly GameOptions Base = new(false);
    public static readonly GameOptions Seasons = new(true);

    /// <summary>Max seats: 8 in extended mode, else 5 with Seasons, else 4 for the base game.</summary>
    public int MaxPlayers => Extended ? 8 : SeasonsOfInis ? 5 : 4;

    /// <summary>Copies of each action card dealt — doubled in extended mode to supply 6–8 hands.</summary>
    public int DeckCopies => Extended ? 2 : 1;

    public string Label => Extended
        ? "Extended (6–8 players)"
        : SeasonsOfInis ? "Seasons of Inis" : "Base game";
}
