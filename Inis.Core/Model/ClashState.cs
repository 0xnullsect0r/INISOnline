namespace Inis.Core.Model;

/// <summary>
/// State of a clash being resolved in a territory. A clash has a Citadels step (other
/// players shelter clans) followed by a Resolution step (players with exposed clans take
/// turns performing one maneuver each) until it ends. Extra clashes spawned by a single
/// effect (e.g. Migration) are queued and resolved one at a time.
/// </summary>
public sealed class ClashState
{
    public required string TerritoryId { get; init; }
    public required string InstigatorId { get; init; }

    /// <summary>Player ids in resolution order, beginning with the instigator.</summary>
    public required List<string> Order { get; init; }

    /// <summary>False during the Citadels step, true once Resolution begins.</summary>
    public bool InResolution { get; set; }

    /// <summary>Clans sheltered in Citadels this clash, by color (protected / not exposed).</summary>
    public Dictionary<ClanColor, int> Sheltered { get; init; } = new();

    /// <summary>Cursor into <see cref="Order"/> for whichever step is active.</summary>
    public int Cursor { get; set; }

    /// <summary>Players who have agreed (this go-around) to end the clash peacefully.</summary>
    public HashSet<string> AgreedToEnd { get; init; } = new();

    /// <summary>When an Attack is pending a response: who attacked and who must answer.</summary>
    public string? PendingAttackerId { get; set; }
    public string? PendingTargetId { get; set; }

    /// <summary>Further territories needing a clash after this one resolves.</summary>
    public List<string> QueuedTerritories { get; init; } = new();

    /// <summary>Set once the Festival's "initiator loses a clan" has been applied.</summary>
    public bool FestivalApplied { get; set; }

    /// <summary>Lug's Spear: no further Triskel reactions may be played this clash.</summary>
    public bool TriskelsBlocked { get; set; }

    /// <summary>Warlord's choice of who performs the first Resolution maneuver, if set.</summary>
    public string? ForcedFirstManeuverId { get; set; }

    /// <summary>Coalition: the two allied movers — they cannot use Citadels nor attack each other.</summary>
    public List<string> CoalitionPlayerIds { get; init; } = new();

    /// <summary>Clans removed during this clash, by color (Dagda's Cauldron can return them).</summary>
    public Dictionary<ClanColor, int> RemovedClans { get; init; } = new();

    public int ShelteredTotal => Sheltered.Values.Sum();
}
