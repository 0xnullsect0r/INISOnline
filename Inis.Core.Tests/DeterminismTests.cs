using Inis.Core.Model;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

public class DeterminismTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i])).ToList();
    }

    /// <summary>Plays a game by always taking the first legal move — fully determined by the seed.</summary>
    private static GameEngine Playthrough(int seed, int n, int maxMoves = 1500)
    {
        var engine = GameEngine.Create("g", seed, Seats(n));
        var moves = 0;
        while (engine.State.Phase != GamePhase.GameOver && moves < maxMoves)
        {
            var legal = engine.LegalMoves();
            if (legal.Count == 0) break;
            engine.Apply(legal[0]);
            moves++;
        }
        return engine;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2024)]
    public void Same_Seed_Replays_To_An_Identical_Intent_Log(int seed)
    {
        var a = Playthrough(seed, 4);
        var b = Playthrough(seed, 4);
        Assert.Equal(a.State.IntentLog, b.State.IntentLog);
    }

    [Fact]
    public void Same_Seed_Reaches_An_Identical_Final_Board()
    {
        var a = Playthrough(7, 3);
        var b = Playthrough(7, 3);

        Assert.Equal(a.State.Phase, b.State.Phase);
        Assert.Equal(a.State.RoundNumber, b.State.RoundNumber);
        Assert.Equal(
            a.State.Territories.Select(kv => (kv.Key, kv.Value.TotalClans, kv.Value.Sanctuaries, kv.Value.Citadels)),
            b.State.Territories.Select(kv => (kv.Key, kv.Value.TotalClans, kv.Value.Sanctuaries, kv.Value.Citadels)));
    }

    [Fact]
    public void A_Full_Game_Makes_Progress_Without_Throwing()
    {
        var engine = Playthrough(123, 4);
        Assert.True(engine.State.IntentLog.Count > 0);
    }
}
