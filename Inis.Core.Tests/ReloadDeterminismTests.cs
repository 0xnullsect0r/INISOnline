using Inis.Core.Model;
using Inis.Core.Net;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

/// <summary>
/// Guards the Phase 5 server-reload determinism fix: a game serialized mid-play and
/// reconstructed (as the server does when reloading a persisted game from the database)
/// must continue producing the <em>identical</em> deterministic sequence. This exercises
/// both the persisted RNG cursor and the persisted draft leftover deck.
/// </summary>
public class ReloadDeterminismTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i])).ToList();
    }

    /// <summary>Advances an engine by the first legal move up to <paramref name="steps"/> times.</summary>
    private static int Step(GameEngine e, int steps)
    {
        var taken = 0;
        while (taken < steps && e.State.Phase != GamePhase.GameOver)
        {
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
            taken++;
        }
        return taken;
    }

    private static void PlayToEnd(GameEngine e, int maxMoves = 2000) => Step(e, maxMoves);

    [Theory]
    [InlineData(1, 2, 12)]   // 2-player exercises the second sub-draft / leftover deck
    [InlineData(42, 3, 20)]
    [InlineData(2024, 4, 30)]
    [InlineData(7, 4, 5)]    // reload very early, during the opening draft
    public void Reloaded_Game_Continues_Identically(int seed, int players, int reloadAfter)
    {
        // Reference engine: never reloaded.
        var reference = GameEngine.Create("g", seed, Seats(players));
        Step(reference, reloadAfter);

        // Reload path: an independent engine played to the same point, then serialized,
        // deserialized, and rebuilt from the restored state.
        var live = GameEngine.Create("g", seed, Seats(players));
        Step(live, reloadAfter);

        var json = InisJson.SerializeState(live.State);
        var restored = InisJson.DeserializeState(json);
        var reloaded = new GameEngine(restored);

        // From the reload point, both engines must evolve identically.
        PlayToEnd(reference);
        PlayToEnd(reloaded);

        Assert.Equal(reference.State.IntentLog, reloaded.State.IntentLog);
        Assert.Equal(reference.State.Phase, reloaded.State.Phase);
        Assert.Equal(reference.State.RoundNumber, reloaded.State.RoundNumber);
        Assert.Equal(reference.State.WinnerId, reloaded.State.WinnerId);
        Assert.Equal(
            reference.State.Territories.OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value.TotalClans, kv.Value.Sanctuaries, kv.Value.Citadels)),
            reloaded.State.Territories.OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value.TotalClans, kv.Value.Sanctuaries, kv.Value.Citadels)));
    }

    [Fact]
    public void State_Round_Trips_Through_Json_Without_Loss()
    {
        var e = GameEngine.Create("g", 99, Seats(3));
        Step(e, 15);

        var restored = InisJson.DeserializeState(InisJson.SerializeState(e.State));

        Assert.Equal(e.State.Seed, restored.Seed);
        Assert.Equal(e.State.RngCursor, restored.RngCursor);
        Assert.Equal(e.State.Players.Count, restored.Players.Count);
        Assert.Equal(e.State.Pending?.Kind, restored.Pending?.Kind);
        Assert.Equal(e.State.Pending?.PlayerId, restored.Pending?.PlayerId);
        Assert.Equal(
            e.State.Players.Select(p => (p.PlayerId, p.Color, p.Hand.Count, p.ClanReserve)),
            restored.Players.Select(p => (p.PlayerId, p.Color, p.Hand.Count, p.ClanReserve)));
    }
}
