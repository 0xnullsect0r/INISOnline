using Inis.Core.Model;
using Inis.Core.Rules;

namespace Inis.Core.Tests;

/// <summary>Builders for hand-crafted engine states so individual rules can be exercised in isolation.</summary>
internal static class EngineTestHelpers
{
    private static readonly ClanColor[] Colors =
        { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };

    public static GameState NewState(int seats, int seed = 1)
    {
        var s = new GameState { GameId = "g", Seed = seed, PretendersRemaining = seats };
        for (var i = 0; i < seats; i++)
            s.Players.Add(new PlayerState
            {
                PlayerId = $"p{i}", DisplayName = $"P{i}", Color = Colors[i], ClanReserve = 12,
            });
        return s;
    }

    public static TerritoryState AddTerritory(GameState s, string inst, params (ClanColor color, int n)[] clans)
    {
        var t = new TerritoryState { InstanceId = inst, DefinitionId = "territory.plains" };
        foreach (var (c, n) in clans) { t.AddClans(c, n); s.PlayerByColor(c)!.ClanReserve -= n; }
        s.Territories[inst] = t;
        return t;
    }

    public static void Link(TerritoryState a, TerritoryState b)
    {
        a.Adjacent.Add(b.InstanceId);
        b.Adjacent.Add(a.InstanceId);
    }

    /// <summary>Wraps a state as a Season-phase engine with the Brenn already having opened.</summary>
    public static GameEngine SeasonEngine(GameState s, int brenn = 0, int current = -1, bool opened = true)
    {
        s.Phase = GamePhase.Season;
        s.BrennIndex = brenn;
        s.CurrentPlayerIndex = current < 0 ? brenn : current;
        s.BrennHasOpened = opened;
        s.Pending = new PendingDecision { Kind = PendingKind.SeasonTurn, PlayerId = s.Players[s.CurrentPlayerIndex].PlayerId };
        return new GameEngine(s);
    }
}
