using Inis.Core.Ai;
using Inis.Core.Model;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

/// <summary>
/// Uses the heuristic AI to play full games, which doubles as an end-to-end soak test of
/// the Phase 2 rules engine: across many seeds and player counts the engine must never
/// throw, must always offer a legal decision until the game ends, and must conserve clans.
/// </summary>
public class AiSimulationTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow, ClanColor.White };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i], IsAi: true)).ToList();
    }

    /// <summary>Total clans for a color on the board.</summary>
    private static int OnBoard(GameState s, ClanColor c) => s.Territories.Values.Sum(t => t.ClansOf(c));

    private static void AssertInvariants(GameEngine e)
    {
        var s = e.State;
        // Clan conservation: every player's clans are either on the board or in reserve.
        foreach (var p in s.Players)
            Assert.True(OnBoard(s, p.Color) + p.ClanReserve == 12,
                $"Clan conservation broken for {p.PlayerId}: board+reserve != 12.");
        // No negative clan stacks ever persist.
        Assert.All(s.Territories.Values, t => Assert.All(t.Clans.Values, v => Assert.True(v >= 0)));
        // If the game is still going, someone must be on the clock with at least one legal move.
        if (s.Phase != GamePhase.GameOver)
        {
            Assert.NotNull(e.Pending);
            Assert.NotEmpty(e.LegalMoves());
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Ai_Plays_Many_Games_Without_Breaking_The_Engine(int players)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var engine = GameEngine.Create("g", seed, Seats(players));
            AiRunner.PlayToEnd(engine, maxMoves: 4000, onStep: AssertInvariants);
            AssertInvariants(engine);
        }
    }

    [Fact]
    public void Same_Seed_And_Ai_Produce_Identical_Games()
    {
        var a = GameEngine.Create("g", 12345, Seats(4));
        var b = GameEngine.Create("g", 12345, Seats(4));
        AiRunner.PlayToEnd(a, maxMoves: 4000);
        AiRunner.PlayToEnd(b, maxMoves: 4000);

        Assert.Equal(a.State.WinnerId, b.State.WinnerId);
        Assert.Equal(a.State.IntentLog, b.State.IntentLog);
    }

    /// <summary>Every card with reactive (Triskel) behaviour, plus the assembly/turn hooks.</summary>
    private static readonly string[] ReactiveCards =
    {
        "action.geis", "action.warlord", "action.bard", "action.raid", "action.master_craftsman",
        "epic.lug_samildanach", "epic.lugs_spear", "epic.tale_of_cuchulain", "epic.ogmas_eloquence",
        "epic.the_fianna", "epic.the_dagda", "epic.battle_frenzy", "epic.dagdas_club",
        "epic.dagdas_cauldron", "epic.diarmuid_grainne", "epic.strengs_resolve",
        "epic.oengus_ploy", "epic.cathbads_word",
    };

    [Fact]
    public void Triskel_Stacked_Games_Never_Deadlock()
    {
        // Force reaction windows to open constantly: every reactive card is dealt out
        // round-robin at the start, then the AI plays to the end. The step invariant
        // (someone always has a legal move) is the deadlock guard.
        for (var seed = 0; seed < 25; seed++)
        {
            var engine = GameEngine.Create("g", seed, Seats(3));
            for (var i = 0; i < ReactiveCards.Length; i++)
                Debug.DebugCommandApi.Apply(engine, new Moves.Move
                {
                    Type = MoveType.Debug, DebugCommand = "grant",
                    PlayerId = $"p{i % 3}", CardId = ReactiveCards[i],
                });
            AiRunner.PlayToEnd(engine, maxMoves: 6000, onStep: AssertInvariants);
            AssertInvariants(engine);
        }
    }

    [Fact]
    public void Some_Games_Reach_A_Winner()
    {
        // Not every capped run must finish, but across many seeds the AI should be able to
        // actually win games — a guard against the engine never reaching a victory state.
        var wins = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            var engine = GameEngine.Create("g", seed, Seats(3));
            AiRunner.PlayToEnd(engine, maxMoves: 4000);
            if (engine.State.Phase == GamePhase.GameOver && engine.State.WinnerId is not null) wins++;
        }
        Assert.True(wins > 0, "No AI game ever reached a winner across 40 seeds.");
    }
}
