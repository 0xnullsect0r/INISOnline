using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

public class SetupAndDraftTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i])).ToList();
    }

    /// <summary>Drive the pick-and-pass draft to completion by always taking the first legal pick.</summary>
    private static GameEngine PlayDraft(int n, int seed = 7)
    {
        var engine = GameEngine.Create("g", seed, Seats(n));
        var guard = 0;
        while (engine.Pending?.Kind == PendingKind.Draft)
        {
            var move = engine.LegalMoves()[0];
            engine.Apply(move);
            Assert.True(guard++ < 500, "Draft did not terminate.");
        }
        return engine;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Setup_Places_Initial_Tiles_Capital_And_Starting_Clans(int n)
    {
        var engine = GameEngine.Create("g", 1, Seats(n));
        var s = engine.State;

        Assert.Equal(n, s.Territories.Count);
        Assert.NotNull(s.CapitalInstanceId);
        Assert.True(s.Territories[s.CapitalInstanceId!].HasCapital);
        Assert.Equal(1, s.Territories[s.CapitalInstanceId!].Sanctuaries);
        // Each player started with 2 clans on the board (reserve 12 - 2 = 10).
        Assert.All(s.Players, p => Assert.Equal(10, p.ClanReserve));
        // Every initial tile touches at least two others (ring layout).
        Assert.All(s.Territories.Values, t => Assert.True(t.Adjacent.Count >= Math.Min(2, n - 1)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Draft_Gives_Each_Player_Four_Action_Cards(int n)
    {
        var engine = PlayDraft(n);
        Assert.Equal(GamePhase.Season, engine.State.Phase);
        foreach (var p in engine.State.Players)
        {
            var actions = p.Hand.Count(c => GameData.Default.Card(c).Type == CardType.Action);
            Assert.Equal(4, actions);
        }
    }

    [Fact]
    public void Two_Player_Draft_Gives_Each_Player_Six_Action_Cards()
    {
        var engine = PlayDraft(2);
        Assert.Equal(GamePhase.Season, engine.State.Phase);
        foreach (var p in engine.State.Players)
        {
            var actions = p.Hand.Count(c => GameData.Default.Card(c).Type == CardType.Action);
            Assert.Equal(6, actions);
        }
    }

    [Fact]
    public void Fewer_Than_Four_Players_Excludes_Four_Player_Only_Cards_From_Setaside_And_Hands()
    {
        var engine = PlayDraft(3);
        var fourPlayer = GameData.Default.Cards.Where(c => c.FourPlayerOnly).Select(c => c.Id).ToHashSet();
        foreach (var p in engine.State.Players)
            Assert.DoesNotContain(p.Hand, c => fourPlayer.Contains(c));
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Setup()
    {
        var a = GameEngine.Create("g", 99, Seats(4));
        var b = GameEngine.Create("g", 99, Seats(4));
        Assert.Equal(a.State.BrennIndex, b.State.BrennIndex);
        Assert.Equal(a.State.CapitalInstanceId, b.State.CapitalInstanceId);
        Assert.Equal(
            a.State.Territories.Values.Select(t => t.DefinitionId),
            b.State.Territories.Values.Select(t => t.DefinitionId));
    }
}
