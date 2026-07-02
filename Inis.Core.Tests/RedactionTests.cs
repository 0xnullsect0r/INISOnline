using Inis.Core.Model;
using Inis.Core.Net;
using Inis.Core.Rules;
using Xunit;

namespace Inis.Core.Tests;

/// <summary>
/// The per-player redaction is the anti-cheat boundary: a player's view must reveal their own
/// hidden information and only the <em>counts</em> of everyone else's.
/// </summary>
public class RedactionTests
{
    private static IReadOnlyList<SeatConfig> Seats(int n)
    {
        var colors = new[] { ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow };
        return Enumerable.Range(0, n).Select(i => new SeatConfig($"p{i}", $"P{i}", colors[i])).ToList();
    }

    private static GameEngine MidGame(int seed = 5, int players = 4, int steps = 40)
    {
        var e = GameEngine.Create("g", seed, Seats(players));
        for (var i = 0; i < steps && e.State.Phase != GamePhase.GameOver; i++)
        {
            var legal = e.LegalMoves();
            if (legal.Count == 0) break;
            e.Apply(legal[0]);
        }
        return e;
    }

    [Fact]
    public void Recipient_Sees_Own_Hand_But_Only_Opponent_Counts()
    {
        var e = MidGame();
        var me = e.State.Players[0];
        var view = PlayerView.Redact(e.State, me.PlayerId);

        var mine = view.Players.First(p => p.PlayerId == me.PlayerId);
        Assert.Equal(me.Hand, mine.Hand); // my own hand is intact

        foreach (var opp in view.Players.Where(p => p.PlayerId != me.PlayerId))
        {
            var real = e.State.Players.First(p => p.PlayerId == opp.PlayerId);
            Assert.Equal(real.Hand.Count, opp.Hand.Count);              // count preserved
            Assert.All(opp.Hand, c => Assert.Equal(PlayerView.Hidden, c)); // contents hidden
        }
    }

    [Fact]
    public void Hidden_Draw_Zones_And_Intent_Log_Are_Masked()
    {
        var e = MidGame();
        var view = PlayerView.Redact(e.State, e.State.Players[0].PlayerId);

        Assert.All(view.EpicDeck, c => Assert.Equal(PlayerView.Hidden, c));
        Assert.All(view.ActionDeck, c => Assert.Equal(PlayerView.Hidden, c));
        Assert.Empty(view.IntentLog);
        if (e.State.SetAsideActionCard is not null)
            Assert.Equal(PlayerView.Hidden, view.SetAsideActionCard);
    }

    [Fact]
    public void Face_Up_Advantages_Are_Public_For_Everyone()
    {
        var e = MidGame();
        var holder = e.State.Players[1];
        holder.Advantages.Add("advantage.forest"); // face-up zone, public information

        var view = PlayerView.Redact(e.State, e.State.Players[0].PlayerId);
        var seen = view.Players.First(p => p.PlayerId == holder.PlayerId);
        Assert.Contains("advantage.forest", seen.Advantages);

        var spectator = PlayerView.Redact(e.State, recipientId: null);
        Assert.Contains("advantage.forest",
            spectator.Players.First(p => p.PlayerId == holder.PlayerId).Advantages);
    }

    [Fact]
    public void Spectator_Sees_No_Hand_Contents()
    {
        var e = MidGame();
        var view = PlayerView.Redact(e.State, recipientId: null);
        foreach (var p in view.Players.Where(p => p.Hand.Count > 0))
            Assert.All(p.Hand, c => Assert.Equal(PlayerView.Hidden, c));
    }

    [Fact]
    public void Redaction_Does_Not_Mutate_The_Authoritative_State()
    {
        var e = MidGame();
        var before = InisJson.SerializeState(e.State);
        _ = PlayerView.Redact(e.State, e.State.Players[1].PlayerId);
        Assert.Equal(before, InisJson.SerializeState(e.State));
    }
}
