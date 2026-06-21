namespace Inis.Core.Model;

/// <summary>Runtime state for one territory currently on the island.</summary>
public sealed class TerritoryState
{
    /// <summary>Instance id (a board may, with Exploration, hold more than one tile of a kind in theory).</summary>
    public required string InstanceId { get; init; }

    /// <summary>Definition id (<see cref="Data.TerritoryDefinition.Id"/>).</summary>
    public required string DefinitionId { get; init; }

    /// <summary>Clans present, keyed by clan color.</summary>
    public Dictionary<ClanColor, int> Clans { get; } = new();

    public int Sanctuaries { get; set; }
    public int Citadels { get; set; }
    public bool HasCapital { get; set; }

    /// <summary>Instance ids of adjacent territories.</summary>
    public HashSet<string> Adjacent { get; } = new();

    public int TotalClans => Clans.Values.Sum();

    public int ClansOf(ClanColor color) => Clans.TryGetValue(color, out var n) ? n : 0;

    public void AddClans(ClanColor color, int amount)
    {
        Clans[color] = ClansOf(color) + amount;
        if (Clans[color] <= 0) Clans.Remove(color);
    }

    public bool IsPresent(ClanColor color) => ClansOf(color) > 0;

    /// <summary>
    /// The chieftain is the player with strictly the most clans here. Returns null on
    /// a tie or an empty territory. (Tie-break nuances are applied by the rules layer.)
    /// </summary>
    public ClanColor? Chieftain()
    {
        if (Clans.Count == 0) return null;
        var ordered = Clans.OrderByDescending(kv => kv.Value).ToList();
        if (ordered.Count == 1) return ordered[0].Key;
        return ordered[0].Value > ordered[1].Value ? ordered[0].Key : null;
    }
}
