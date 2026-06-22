using Inis.Core.Model;
using Inis.Core.Moves;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

public class SeasonLoopTests
{
    [Fact]
    public void Brenn_Cannot_Pass_Before_Opening_The_Season()
    {
        var s = NewState(3);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        var engine = SeasonEngine(s, brenn: 0, opened: false);
        Assert.Throws<InvalidOperationException>(() => engine.Apply(Move.Pass("p0")));
    }

    [Fact]
    public void Consecutive_Passes_By_All_Players_End_The_Season_And_Start_A_New_Round()
    {
        var s = NewState(3);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.CapitalInstanceId = "T0";
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.State.RoundNumber = 1;

        engine.Apply(Move.Pass("p0"));
        engine.Apply(Move.Pass("p1"));
        engine.Apply(Move.Pass("p2"));

        // Season ended -> new Assembly -> draft of the next round.
        Assert.Equal(2, engine.State.RoundNumber);
        Assert.Equal(PendingKind.Draft, engine.Pending!.Kind);
    }

    [Fact]
    public void Playing_A_Card_Resets_The_Consecutive_Pass_Counter()
    {
        var s = NewState(3);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[1].Hand.Add("action.new_clans");
        var engine = SeasonEngine(s, brenn: 0, opened: true);

        engine.Apply(Move.Pass("p0")); // p0 (already opened) passes -> 1
        // p1's turn: play a card -> counter resets to 0
        engine.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p1", CardId = "action.new_clans", TerritoryId = "T0" });
        Assert.Equal(0, engine.State.ConsecutivePasses);
    }

    [Fact]
    public void Take_Pretender_Requires_Meeting_A_Condition()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1)); // Red present in only one territory: meets nothing
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        Assert.Throws<InvalidOperationException>(() =>
            engine.Apply(new Move { Type = MoveType.TakePretender, PlayerId = "p0" }));
    }

    [Fact]
    public void Take_Pretender_Succeeds_When_A_Condition_Is_Met()
    {
        var s = NewState(2);
        for (var i = 0; i < 6; i++) AddTerritory(s, $"T{i}", (ClanColor.Red, 1)); // Land: 6 territories
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.Apply(new Move { Type = MoveType.TakePretender, PlayerId = "p0" });
        Assert.True(engine.State.Players[0].HasPretenderToken);
        Assert.Equal(1, engine.State.PretendersRemaining); // 2 seats -> 1 left
    }
}
