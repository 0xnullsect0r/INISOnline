using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

/// <summary>
/// Phase 10: the Seasons of Inis content toggle. The base game must never deal expansion cards;
/// a Seasons game adds them (and enables the 5th seat), and both play to completion.
/// </summary>
public class SeasonsExpansionTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i], IsAi: true)).ToList();
    }

    private static IEnumerable<string> AllHeldActionCards(GameEngine e)
    {
        // Collect every action card any player could see across the whole game.
        var seen = new HashSet<string>();
        var moves = 0;
        while (e.State.Phase != GamePhase.GameOver && moves < 4000)
        {
            foreach (var p in e.State.Players)
                foreach (var c in p.Hand)
                    if (GameData.Default.TryGetCard(c, out var d) && d.Type == CardType.Action) seen.Add(c);
            if (e.State.Draft is { } draft)
                foreach (var hand in draft.Hands)
                    foreach (var c in hand) seen.Add(c);
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
            moves++;
        }
        return seen;
    }

    private static readonly string[] SeasonsCardIds =
        GameData.Default.Cards.Where(c => c.Expansion is not null).Select(c => c.Id).ToArray();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Base_Game_Never_Deals_Expansion_Cards(int players)
    {
        var e = GameEngine.Create("g", 1, Seats(players)); // base by default
        var seen = AllHeldActionCards(e);
        Assert.DoesNotContain(seen, c => SeasonsCardIds.Contains(c));
    }

    [Fact]
    public void Seasons_Game_Includes_Expansion_Cards()
    {
        // Across several seeds the expansion cards must actually appear in a Seasons game.
        var anySeasons = false;
        for (var seed = 0; seed < 8 && !anySeasons; seed++)
        {
            var e = GameEngine.Create("g", seed, Seats(4), options: GameOptions.Seasons);
            anySeasons = AllHeldActionCards(e).Any(c => SeasonsCardIds.Contains(c));
        }
        Assert.True(anySeasons, "No Seasons of Inis card ever appeared in a Seasons game.");
    }

    [Fact]
    public void Seasons_Replaces_Base_Exploration_And_Druid()
    {
        var e = GameEngine.Create("g", 3, Seats(4), options: GameOptions.Seasons);
        var seen = AllHeldActionCards(e).ToHashSet();
        // The base variants are replaced by the _seasons versions, so they never appear.
        Assert.DoesNotContain("action.exploration", seen);
        Assert.DoesNotContain("action.druid", seen);
    }

    [Fact]
    public void Base_Game_Rejects_A_Fifth_Player()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameEngine.Create("g", 1, Seats(5)));
    }

    [Fact]
    public void Seasons_Supports_Five_Players_To_Completion()
    {
        var e = GameEngine.Create("g", 1, Seats(5), options: GameOptions.Seasons);
        var moves = 0;
        while (e.State.Phase != GamePhase.GameOver && moves < 4000)
        {
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
            moves++;
        }
        Assert.True(e.State.IntentLog.Count > 0);
        Assert.Equal(5, e.State.Players.Count);
    }

    [Fact]
    public void Options_Round_Trip_Through_Json()
    {
        var e = GameEngine.Create("g", 9, Seats(5), options: GameOptions.Seasons);
        var restored = Net.InisJson.DeserializeState(Net.InisJson.SerializeState(e.State));
        Assert.True(restored.Options.SeasonsOfInis);
        Assert.Equal(5, restored.Options.MaxPlayers);
    }
}
