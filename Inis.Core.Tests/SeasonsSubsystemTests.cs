using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

/// <summary>
/// Seasons of Inis deep subsystems (M4): the season wheel and its Sacred Festivals,
/// season-phase modifiers, and Harbours / sea travel.
/// </summary>
public class SeasonsSubsystemTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i], IsAi: true)).ToList();
    }

    /// <summary>A hand-built Seasons-of-Inis state in the given season.</summary>
    private static GameState SeasonsState(int seats, Season season)
    {
        var s = NewState(seats);
        s.Options = GameOptions.Seasons;
        s.CurrentSeason = season;
        return s;
    }

    // ------------------------------------------------------------ the wheel

    [Fact]
    public void Seasons_Games_Start_On_The_Wheel_And_Base_Games_Do_Not()
    {
        var seasons = GameEngine.Create("g", 1, Seats(3), options: GameOptions.Seasons);
        Assert.NotNull(seasons.State.CurrentSeason);

        var baseGame = GameEngine.Create("g", 1, Seats(3));
        Assert.Null(baseGame.State.CurrentSeason);
    }

    [Fact]
    public void The_Season_Marker_Advances_When_A_Season_Ends()
    {
        var s = SeasonsState(2, Season.Summer);
        AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        var e = SeasonEngine(s);

        e.Apply(Move.Pass("p0"));
        e.Apply(Move.Pass("p1"));

        Assert.Equal(Season.Autumn, s.CurrentSeason);
    }

    // ------------------------------------------------------ sacred festivals

    [Fact]
    public void Spring_Festival_Gives_The_Poorest_Hands_A_Clan()
    {
        var s = SeasonsState(2, Season.Winter); // ends -> next Assembly is Spring
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 3));
        s.Players[0].Hand.Add("epic.eriu"); // Red keeps an epic; Blue's hand is empty
        var e = SeasonEngine(s);

        e.Apply(Move.Pass("p0"));
        e.Apply(Move.Pass("p1"));

        // The season ended; the new Assembly ran its draft deal... but festivals fire after
        // the draft. Drive the draft to completion with first legal picks.
        while (e.Pending!.Kind == PendingKind.Draft) e.Apply(e.LegalMoves()[0]);

        // Blue had the fewest cards after the deal? Both drafted equally — the pre-draft epic
        // makes Red's hand strictly larger, so Blue (alone) mustered one clan.
        Assert.Equal(Season.Spring, s.CurrentSeason);
        Assert.Equal(4, t0.ClansOf(ClanColor.Blue));
        Assert.Equal(1, t0.ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Winter_Festival_Lets_A_Player_Trade_An_Action_For_An_Epic()
    {
        var s = SeasonsState(2, Season.Autumn); // ends -> next Assembly is Winter
        AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        s.EpicDeck.Add("epic.balors_eye");
        var e = SeasonEngine(s);

        e.Apply(Move.Pass("p0"));
        e.Apply(Move.Pass("p1"));
        while (e.Pending!.Kind == PendingKind.Draft) e.Apply(e.LegalMoves()[0]);

        // Both players now hold Action cards, so the Samhain window prompts each in turn.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("SacredFestival", e.Pending!.Trigger);
        var reactor = e.Pending!.PlayerId;
        var player = s.PlayerById(reactor)!;
        var actionsBefore = player.Hand.Count;

        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = reactor, CardId = "festival.samhain" });

        Assert.Contains("epic.balors_eye", player.Hand);
        Assert.Equal(actionsBefore, player.Hand.Count); // -1 action, +1 epic

        // The other player passes; the Season begins.
        e.Apply(new Move { Type = MoveType.PassReaction, PlayerId = e.Pending!.PlayerId });
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
    }

    [Fact]
    public void Autumn_Festival_Trades_Epics_For_Clans_And_Caps_The_Epic_Hand_At_Three()
    {
        var s = SeasonsState(2, Season.Summer); // ends -> next Assembly is Autumn
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        s.Players[0].Hand.AddRange(new[]
            { "epic.eriu", "epic.balors_eye", "epic.the_fianna", "epic.tuans_memory", "epic.dagdas_harp" });
        var e = SeasonEngine(s);

        e.Apply(Move.Pass("p0"));
        e.Apply(Move.Pass("p1"));
        while (e.Pending!.Kind == PendingKind.Draft) e.Apply(e.LegalMoves()[0]);

        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        // Red (Brenn-side holder of epics) trades two epics for two clans.
        var reactor = e.Pending!.PlayerId;
        e.Apply(new Move
        {
            Type = MoveType.PlayReaction, PlayerId = reactor, CardId = "festival.lugnasad",
            CardIds = new[] { "epic.eriu", "epic.balors_eye" }, TerritoryId = "T0",
        });

        var red = s.Players[0];
        Assert.Equal(3, t0.ClansOf(ClanColor.Red)); // 1 + 2 mustered
        Assert.DoesNotContain("epic.eriu", red.Hand);

        // Remaining players pass; the 3-epic hand limit then holds for everyone.
        while (e.Pending!.Kind == PendingKind.Reaction)
            e.Apply(new Move { Type = MoveType.PassReaction, PlayerId = e.Pending!.PlayerId });
        foreach (var p in s.Players)
            Assert.True(p.Hand.Count(c => c.StartsWith("epic.")) <= 3);
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
    }

    // -------------------------------------------------- season-phase modifiers

    [Fact]
    public void Winter_Limits_Card_Movement_To_Three_Clans()
    {
        var s = SeasonsState(2, Season.Winter);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 6));
        var t1 = AddTerritory(s, "T1");
        Link(t0, t1);
        s.Players[0].Hand.Add("action.migration");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.migration", FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 6 });

        Assert.Equal(3, t1.ClansOf(ClanColor.Red)); // clamped by Samhain
        Assert.Equal(3, t0.ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Summer_Allows_Discarding_An_Action_Card_To_Move_Clans()
    {
        var s = SeasonsState(2, Season.Summer);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 4));
        var t1 = AddTerritory(s, "T1");
        Link(t0, t1);
        s.Players[0].Hand.Add("action.bard");
        var e = SeasonEngine(s);

        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.SummerMove);
        e.Apply(new Move
        {
            Type = MoveType.SummerMove, PlayerId = "p0", CardIds = new[] { "action.bard" },
            FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 3,
        });

        Assert.Equal(3, t1.ClansOf(ClanColor.Red));
        Assert.Contains("action.bard", s.ActionDiscard);
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
    }

    // ------------------------------------------------------------- sea travel

    [Fact]
    public void Harbours_Connect_Distant_Territories_For_Movement()
    {
        var s = SeasonsState(2, Season.Spring);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 3));
        var t1 = AddTerritory(s, "T1", (ClanColor.Blue, 1)); // NOT adjacent to T0
        t0.HasHarbour = true;
        t1.HasHarbour = true;
        s.Players[0].Hand.Add("action.migration");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.migration", FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 2 });

        Assert.Equal(2, t1.ClansOf(ClanColor.Red)); // sailed across
        Assert.NotNull(s.ActiveClash);              // and clashed on arrival
    }

    [Fact]
    public void Sea_Travel_Requires_The_Seasons_Expansion()
    {
        var s = NewState(2); // base game
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 3));
        var t1 = AddTerritory(s, "T1");
        t0.HasHarbour = true;
        t1.HasHarbour = true;
        s.Players[0].Hand.Add("action.migration");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.migration", FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 2 });

        Assert.Equal(0, t1.ClansOf(ClanColor.Red)); // no sea lanes in the base game
    }

    [Fact]
    public void The_Capital_Gets_A_Harbour_In_Seasons_Games()
    {
        var e = GameEngine.Create("g", 5, Seats(3), options: GameOptions.Seasons);
        var capital = e.State.Territories[e.State.CapitalInstanceId!];
        Assert.True(capital.HasHarbour);

        var baseGame = GameEngine.Create("g", 5, Seats(3));
        Assert.False(baseGame.State.Territories[baseGame.State.CapitalInstanceId!].HasHarbour);
    }

    [Fact]
    public void Explored_Islands_Sit_At_Sea_With_A_Harbour()
    {
        var s = SeasonsState(2, Season.Spring);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 2));
        AddTerritory(s, "T1", (ClanColor.Blue, 1));
        s.Players[0].Hand.Add("action.exploration_seasons");
        var e = SeasonEngine(s);

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.exploration_seasons",
            TerritoryId = "T0", CardIds = new[] { "territory.hy_brasil" },
        });

        var island = s.Territories.Values.First(t => t.DefinitionId == "territory.hy_brasil");
        Assert.True(island.HasHarbour);
        Assert.Empty(island.Adjacent);                     // touches nothing
        Assert.Equal(1, island.ClansOf(ClanColor.Red));    // the explorer landed a clan
    }

    [Fact]
    public void Base_Games_Never_Draw_Expansion_Territories()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 2));
        s.Players[0].Hand.Add("action.exploration");
        var e = SeasonEngine(s);

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.exploration",
            TerritoryId = "T0", CardIds = new[] { "territory.hy_brasil" },
        });

        Assert.DoesNotContain(s.Territories.Values, t => t.DefinitionId == "territory.hy_brasil");
    }

    // ----------------------------------------------------- island advantages

    [Fact]
    public void Hy_Brasil_Advantage_Counts_As_A_Deed_At_The_Victory_Check()
    {
        var s = NewState(2);
        // Red is present in 5 territories: one short of the Land condition.
        for (var i = 0; i < 5; i++) AddTerritory(s, $"T{i}", (ClanColor.Red, 1));
        var red = s.Players[0];

        Assert.False(VictoryEvaluator.MeetsAny(s, red));
        red.Advantages.Add("advantage.hy_brasil");
        Assert.True(VictoryEvaluator.MeetsAny(s, red));
    }

    [Fact]
    public void Aber_Advantage_Ferries_A_Clan_Between_Its_Neighbours()
    {
        var s = NewState(2);
        var aber = new TerritoryState { InstanceId = "TA", DefinitionId = "territory.aber" };
        s.Territories["TA"] = aber;
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 2));
        var t1 = AddTerritory(s, "T1");
        Link(aber, t0);
        Link(aber, t1);
        s.Players[0].Advantages.Add("advantage.aber");
        var e = SeasonEngine(s);

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "advantage.aber",
            FromTerritoryId = "T0", ToTerritoryId = "T1",
        });

        Assert.Equal(1, t0.ClansOf(ClanColor.Red));
        Assert.Equal(1, t1.ClansOf(ClanColor.Red));
    }

    // ------------------------------------------------------------- durability

    [Fact]
    public void Five_Player_Seasons_Game_With_Festivals_Runs_To_Completion()
    {
        for (var seed = 0; seed < 5; seed++)
        {
            var e = GameEngine.Create("g", seed, Seats(5), options: GameOptions.Seasons);
            var moves = 0;
            while (e.State.Phase != GamePhase.GameOver && moves < 6000)
            {
                var move = Ai.HeuristicAi.ChooseMove(e);
                e.Apply(move);
                moves++;
            }
            Assert.True(moves > 0);
        }
    }
}
