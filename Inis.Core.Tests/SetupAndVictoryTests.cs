using Inis.Core.Model;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

public class SetupAndVictoryTests
{
    private static IReadOnlyList<SeatConfig> ThreeSeats() => new[]
    {
        new SeatConfig("p1", "Aoife", ClanColor.Red),
        new SeatConfig("p2", "Brian", ClanColor.Blue),
        new SeatConfig("p3", "Ciara", ClanColor.Green),
    };

    [Fact]
    public void Create_Builds_Seats_And_Shuffles_Decks()
    {
        var state = GameSetup.Create("g1", seed: 42, ThreeSeats());
        Assert.Equal(3, state.Players.Count);
        Assert.NotEmpty(state.ActionDeck);
        Assert.NotEmpty(state.EpicDeck);
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Deck_Order()
    {
        var a = GameSetup.Create("g", 7, ThreeSeats());
        var b = GameSetup.Create("g", 7, ThreeSeats());
        Assert.Equal(a.ActionDeck, b.ActionDeck);
        Assert.Equal(a.EpicDeck, b.EpicDeck);
    }

    [Fact]
    public void Create_Rejects_Invalid_Player_Counts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameSetup.Create("g", 1, new[] { new SeatConfig("p1", "Solo", ClanColor.Red) }));
    }

    [Fact]
    public void Land_Victory_Counts_Distinct_Territories_With_Presence()
    {
        var state = GameSetup.Create("g", 1, ThreeSeats());
        for (var i = 0; i < 6; i++)
        {
            var t = new TerritoryState { InstanceId = $"t{i}", DefinitionId = "d" };
            t.AddClans(ClanColor.Red, 1);
            state.Territories[t.InstanceId] = t;
        }

        Assert.Equal(6, VictoryEvaluator.LandValue(state, ClanColor.Red));
        Assert.True(VictoryEvaluator.MeetsAny(state, state.PlayerByColor(ClanColor.Red)!));
    }

    [Fact]
    public void Religion_Victory_Counts_Sanctuaries_Where_Present()
    {
        var state = GameSetup.Create("g", 1, ThreeSeats());
        var t = new TerritoryState { InstanceId = "t", DefinitionId = "d", Sanctuaries = 5 };
        t.AddClans(ClanColor.Blue, 1);
        state.Territories[t.InstanceId] = t;

        Assert.Equal(5, VictoryEvaluator.ReligionValue(state, ClanColor.Blue));
        // 5 sanctuaries + 1 deed (wild) reaches the threshold of 6.
        state.PlayerByColor(ClanColor.Blue)!.Deeds = 1;
        Assert.True(VictoryEvaluator.MeetsAny(state, state.PlayerByColor(ClanColor.Blue)!));
    }

    [Fact]
    public void Leadership_Counts_Opponent_Clans_In_Territories_You_Lead()
    {
        var state = GameSetup.Create("g", 1, ThreeSeats());
        var t = new TerritoryState { InstanceId = "t", DefinitionId = "d" };
        t.AddClans(ClanColor.Red, 3);   // Red is chieftain
        t.AddClans(ClanColor.Blue, 2);  // opponents present
        state.Territories[t.InstanceId] = t;

        Assert.Equal(2, VictoryEvaluator.LeadershipValue(state, ClanColor.Red));
        Assert.Equal(0, VictoryEvaluator.LeadershipValue(state, ClanColor.Blue)); // not chieftain
    }
}
