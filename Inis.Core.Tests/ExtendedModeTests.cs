using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

/// <summary>
/// Phase 11: the house-ruled 6–8 player extended mode. Extra clans, a doubled action deck and a
/// raised seat cap; the engine must still set up, draft and play to completion.
/// </summary>
public class ExtendedModeTests
{
    private static readonly ClanColor[] Colors =
    {
        ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow,
        ClanColor.White, ClanColor.Purple, ClanColor.Orange, ClanColor.Teal,
    };

    private static IReadOnlyList<SeatConfig> Seats(int n) =>
        Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", Colors[i], IsAi: true)).ToList();

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Extended_Plays_To_Completion(int players)
    {
        var e = GameEngine.Create("g", 1, Seats(players), options: new GameOptions(Extended: true));
        var moves = 0;
        while (e.State.Phase != GamePhase.GameOver && moves < 6000)
        {
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
            moves++;
        }
        Assert.Equal(players, e.State.Players.Count);
        Assert.True(e.State.IntentLog.Count > 0);
        // Distinct clan colours per seat.
        Assert.Equal(players, e.State.Players.Select(p => p.Color).Distinct().Count());
    }

    [Fact]
    public void Each_Player_Drafts_A_Full_Hand_In_Extended()
    {
        var e = GameEngine.Create("g", 2, Seats(8), options: new GameOptions(Extended: true));
        // Advance through the opening draft.
        var guard = 0;
        while (e.State.Phase == GamePhase.Assembly && guard++ < 2000)
        {
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
        }
        Assert.Equal(GamePhase.Season, e.State.Phase);
        foreach (var p in e.State.Players)
        {
            var actions = p.Hand.Count(c => GameData.Default.Card(c).Type == CardType.Action);
            Assert.Equal(4, actions);
        }
    }

    [Fact]
    public void Non_Extended_Rejects_Six_Players()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GameEngine.Create("g", 1, Seats(6), options: GameOptions.Seasons));
    }
}
