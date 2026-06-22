using Inis.Core.Model;

namespace Inis.Core.Rules;

/// <summary>Result of evaluating one victory condition for one player.</summary>
public readonly record struct VictoryProgress(VictoryCondition Condition, int Value, int Threshold, bool Met)
{
    public int Remaining => Math.Max(0, Threshold - Value);
}

/// <summary>
/// Pure evaluation of the three victory conditions. Deeds act as wild +1 toward a
/// single condition; here we report raw progress and the deed-boosted "met" status.
/// Final win adjudication (pretenders, ties) is handled by the Assembly phase logic.
/// </summary>
public static class VictoryEvaluator
{
    public const int Threshold = GameSetup.VictoryThreshold;

    public static IReadOnlyList<VictoryProgress> Evaluate(GameState state, PlayerState player)
    {
        var deeds = player.Deeds;
        return new[]
        {
            Progress(VictoryCondition.Leadership, LeadershipValue(state, player.Color), deeds),
            Progress(VictoryCondition.Land, LandValue(state, player.Color), deeds),
            Progress(VictoryCondition.Religion, ReligionValue(state, player.Color), deeds),
        };
    }

    /// <summary>True if the player meets at least one condition (deeds may be spent as wilds).</summary>
    public static bool MeetsAny(GameState state, PlayerState player)
        => CountConditionsMet(state, player) >= 1;

    /// <summary>
    /// Number of victory conditions the player can satisfy at once, allocating Deed tokens
    /// optimally — each Deed adds +1 to a single condition and can complete only one. This is
    /// the value the Assembly victory check compares between pretenders.
    /// </summary>
    public static int CountConditionsMet(GameState state, PlayerState player)
    {
        var shortfalls = new[]
        {
            Math.Max(0, Threshold - LeadershipValue(state, player.Color)),
            Math.Max(0, Threshold - LandValue(state, player.Color)),
            Math.Max(0, Threshold - ReligionValue(state, player.Color)),
        }.OrderBy(s => s).ToArray();

        var deeds = player.Deeds;
        var met = 0;
        foreach (var need in shortfalls)
        {
            if (need == 0) { met++; continue; }
            if (deeds >= need) { deeds -= need; met++; }
        }
        return met;
    }

    private static VictoryProgress Progress(VictoryCondition condition, int rawValue, int deeds)
    {
        // A deed can top up any single condition; the player "meets" a condition if
        // raw value + available deeds reaches the threshold.
        var met = rawValue + deeds >= Threshold;
        return new VictoryProgress(condition, rawValue, Threshold, met);
    }

    /// <summary>Opponents' clans located in territories where this color is chieftain.</summary>
    public static int LeadershipValue(GameState state, ClanColor color)
    {
        var total = 0;
        foreach (var t in state.Territories.Values)
        {
            if (t.Chieftain() != color) continue;
            total += t.Clans.Where(kv => kv.Key != color).Sum(kv => kv.Value);
        }
        return total;
    }

    /// <summary>Number of distinct territories where this color is present.</summary>
    public static int LandValue(GameState state, ClanColor color)
        => state.Territories.Values.Count(t => t.IsPresent(color));

    /// <summary>Sanctuaries in territories where this color is present.</summary>
    public static int ReligionValue(GameState state, ClanColor color)
        => state.Territories.Values.Where(t => t.IsPresent(color)).Sum(t => t.Sanctuaries);
}
