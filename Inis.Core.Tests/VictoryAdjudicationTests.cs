using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

public class VictoryAdjudicationTests
{
    /// <summary>Ends the Season (everyone passes) so the engine runs the Assembly victory check.</summary>
    private static void EndSeasonByPassing(GameEngine engine)
    {
        var guard = 0;
        while (engine.Pending!.Kind == PendingKind.SeasonTurn)
        {
            engine.Apply(Move.Pass(engine.Pending!.PlayerId));
            Assert.True(guard++ < 50);
        }
    }

    [Fact]
    public void Land_Path_Wins_For_A_Pretender_Present_In_Six_Territories()
    {
        var s = NewState(2);
        for (var i = 0; i < 6; i++) AddTerritory(s, $"T{i}", (ClanColor.Red, 1));
        s.CapitalInstanceId = "T0";
        s.Players[0].HasPretenderToken = true;
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        EndSeasonByPassing(engine);
        Assert.Equal(GamePhase.GameOver, engine.State.Phase);
        Assert.Equal("p0", engine.State.WinnerId);
    }

    [Fact]
    public void Religion_Path_Wins_For_A_Pretender_With_Six_Sanctuaries()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 1));
        t.Sanctuaries = 6;
        s.CapitalInstanceId = "T0";
        s.Players[0].HasPretenderToken = true;
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        EndSeasonByPassing(engine);
        Assert.Equal("p0", engine.State.WinnerId);
    }

    [Fact]
    public void Leadership_Path_Wins_For_A_Chieftain_Over_Six_Opposing_Clans()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 7), (ClanColor.Blue, 6));
        s.CapitalInstanceId = "T0";
        s.Players[0].HasPretenderToken = true;
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        EndSeasonByPassing(engine);
        Assert.Equal("p0", engine.State.WinnerId);
    }

    [Fact]
    public void No_Winner_Without_A_Pretender_Token_Even_If_A_Condition_Is_Met()
    {
        var s = NewState(2);
        for (var i = 0; i < 6; i++) AddTerritory(s, $"T{i}", (ClanColor.Red, 1));
        s.CapitalInstanceId = "T0";
        // p0 meets Land but holds NO pretender token.
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        EndSeasonByPassing(engine);
        Assert.NotEqual(GamePhase.GameOver, engine.State.Phase);
        Assert.Null(engine.State.WinnerId);
    }

    [Fact]
    public void Tie_Is_Broken_In_Favor_Of_The_Brenn()
    {
        var s = NewState(3);
        // Both Red (p0) and Blue (p1) are present in 6 territories (one condition each).
        for (var i = 0; i < 6; i++) AddTerritory(s, $"R{i}", (ClanColor.Red, 1));
        for (var i = 0; i < 6; i++) AddTerritory(s, $"B{i}", (ClanColor.Blue, 1));
        s.CapitalInstanceId = "R0"; // Red is capital chieftain -> becomes Brenn at Assembly
        s.Players[0].HasPretenderToken = true;
        s.Players[1].HasPretenderToken = true;
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        EndSeasonByPassing(engine);
        Assert.Equal(GamePhase.GameOver, engine.State.Phase);
        Assert.Equal("p0", engine.State.WinnerId); // Brenn wins the tie
    }

    [Fact]
    public void Deeds_Are_Allocated_So_A_Single_Deed_Completes_Only_One_Condition()
    {
        var s = NewState(2);
        // Present in 5 territories (Land shortfall 1) and 5 sanctuaries (Religion shortfall 1).
        for (var i = 0; i < 5; i++) AddTerritory(s, $"T{i}", (ClanColor.Red, 1));
        s.Territories["T0"].Sanctuaries = 5;
        var p = s.Players[0];

        p.Deeds = 1;
        Assert.Equal(1, VictoryEvaluator.CountConditionsMet(s, p)); // one deed -> one condition
        p.Deeds = 2;
        Assert.Equal(2, VictoryEvaluator.CountConditionsMet(s, p)); // two deeds -> both
    }
}
