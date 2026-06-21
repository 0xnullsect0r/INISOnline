namespace Inis.Core.Model;

/// <summary>The four base-game clans (player factions). Seasons of Inis adds a 5th later.</summary>
public enum ClanColor
{
    Red,
    Blue,
    Green,
    Yellow,
    // Reserved for the Seasons of Inis expansion (Phase 10).
    White,
}

/// <summary>Top-level card categories.</summary>
public enum CardType
{
    Action,
    EpicTale,
    Advantage,
    Reference,
}

/// <summary>The three paths to victory. Threshold is 6 (deeds count as wild +1).</summary>
public enum VictoryCondition
{
    /// <summary>Chieftain in territories together holding at least 6 of opponents' clans.</summary>
    Leadership,
    /// <summary>Present in at least 6 different territories.</summary>
    Land,
    /// <summary>Present in territories containing at least 6 sanctuaries.</summary>
    Religion,
}

/// <summary>Buildings that can sit on a territory.</summary>
public enum BuildingType
{
    Sanctuary,
    Citadel,
    /// <summary>The single special Citadel. Its territory's chieftain becomes the Brenn.</summary>
    Capital,
}

/// <summary>Visual / thematic terrain of a territory tile (affects art, not core rules).</summary>
public enum TerrainType
{
    Plains,
    Forest,
    Mountain,
    Bog,
    Coast,
}

/// <summary>Phases of a round.</summary>
public enum GamePhase
{
    /// <summary>Victory check, chieftain/Brenn determination, then the Action-card draft.</summary>
    Assembly,
    /// <summary>Players take turns: play a card, pass, or take a pretender token.</summary>
    Season,
    /// <summary>A clash is being resolved within the Season phase.</summary>
    Clash,
    GameOver,
}

/// <summary>Sub-steps within the Assembly phase.</summary>
public enum AssemblyStep
{
    VictoryCheck,
    DetermineChieftains,
    Draft,
}

/// <summary>A player's options on their Season-phase turn.</summary>
public enum SeasonAction
{
    PlayCard,
    Pass,
    TakePretender,
}
