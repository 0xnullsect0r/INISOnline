namespace Inis.Core.Model;

/// <summary>The four base-game clans (player factions). Seasons of Inis adds a 5th later.</summary>
public enum ClanColor
{
    Red,
    Blue,
    Green,
    Yellow,
    // 5th clan: Seasons of Inis expansion (Phase 10).
    White,
    // Extra factions for the house-ruled 6–8 player extended mode (Phase 11).
    Purple,
    Orange,
    Teal,
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

/// <summary>What the engine is currently waiting for a player to decide.</summary>
public enum PendingKind
{
    /// <summary>The pick-and-pass Action-card draft.</summary>
    Draft,
    /// <summary>A normal Season turn: play / pass / take pretender.</summary>
    SeasonTurn,
    /// <summary>A clash's Citadels step: shelter a clan or decline.</summary>
    ClashShelter,
    /// <summary>A clash's Resolution step: choose a maneuver.</summary>
    ClashManeuver,
    /// <summary>An attacked player chooses how to absorb an Attack.</summary>
    AttackResponse,
    /// <summary>The game has ended.</summary>
    GameOver,
    /// <summary>A reaction (Triskel) window: play a matching card or pass. Appended for wire compat.</summary>
    Reaction,
}

/// <summary>The kinds of move a player can submit to the engine.</summary>
public enum MoveType
{
    DraftPick,
    PlayCard,
    Pass,
    TakePretender,
    ClashShelter,
    ClashSkipShelter,
    Attack,
    Withdraw,
    EndClash,
    AttackRemoveClan,
    AttackDiscardCard,
    Resign,
    Debug,
    // Reaction-window verbs; appended (never reordered) for wire compatibility.
    PlayReaction,
    PassReaction,
    /// <summary>Seasons of Inis, Summer: discard an Action card to move up to 3 clans instead.</summary>
    SummerMove,
}

/// <summary>
/// Seasons of Inis: the season wheel, in the order the marker advances. Each season is one
/// of the sacred festivals (Imbolc, Beltane, Lugnasad, Samhain).
/// </summary>
public enum Season
{
    /// <summary>Imbolc — at the Assembly, the players with the fewest cards muster a clan.</summary>
    Spring,
    /// <summary>Beltane — during the Season, an Action card may be discarded to move clans.</summary>
    Summer,
    /// <summary>Lugnasad — at the Assembly, Epic Tales may be traded for clans (hand limit 3).</summary>
    Autumn,
    /// <summary>Samhain — Action cards may become Epic Tales; movement is limited to 3 clans.</summary>
    Winter,
}

/// <summary>Direction of turn order, set by the Flock of Crows token each Assembly.</summary>
public enum TurnDirection
{
    Clockwise,
    CounterClockwise,
}
