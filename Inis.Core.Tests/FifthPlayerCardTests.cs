using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

/// <summary>
/// The Seasons of Inis 5th-player Action cards: Fili, Coalition, The King and the Land,
/// and the two-mode Clans Harmony.
/// </summary>
public class FifthPlayerCardTests
{
    // ------------------------------------------------------------------- Fili

    [Fact]
    public void Fili_Prevents_Clashes_In_Its_Territory_Until_The_Season_Ends()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1)); // shared
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.fili");
        s.Players[0].Hand.Add("action.conquest");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.fili", TerritoryId = "T0" });
        Assert.Equal("T0", s.FiliTerritoryId);

        // p1 passes; p0 moves into the shared territory — no clash may start there.
        e.Apply(Move.Pass("p1"));
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        Assert.Null(s.ActiveClash);
        Assert.Equal(GamePhase.Season, s.Phase);
        Assert.Equal(3, t0.ClansOf(ClanColor.Red)); // moved in peacefully

        // Ending the season removes the token.
        e.Apply(Move.Pass("p1"));
        e.Apply(Move.Pass("p0"));
        Assert.Null(s.FiliTerritoryId);
    }

    // -------------------------------------------------------------- Coalition

    private static GameEngine CoalitionSetup(out GameState s)
    {
        s = NewState(3);
        var shared = AddTerritory(s, "T0", (ClanColor.Red, 3), (ClanColor.Blue, 2)); // Red + Blue share
        var dest = AddTerritory(s, "T1", (ClanColor.Green, 1));                       // Green holds the target
        Link(shared, dest);
        s.Players[0].Hand.Add("action.coalition");
        var e = SeasonEngine(s);
        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.coalition",
            FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 2, TargetPlayerId = "p1",
        });
        return e;
    }

    [Fact]
    public void Coalition_Partner_May_Move_Clans_Along_Then_The_Clash_Starts()
    {
        var e = CoalitionSetup(out var s);

        // The instigator's clans moved; the named partner is asked to join.
        Assert.Equal(2, s.Territories["T1"].ClansOf(ClanColor.Red));
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.coalition", Amount = 1 });

        Assert.Equal(1, s.Territories["T1"].ClansOf(ClanColor.Blue));
        var clash = s.ActiveClash!;
        Assert.Equal("T1", clash.TerritoryId);
        Assert.Equal("p0", clash.InstigatorId);
        Assert.Contains("p0", clash.CoalitionPlayerIds);
        Assert.Contains("p1", clash.CoalitionPlayerIds);
    }

    [Fact]
    public void Coalition_Partners_Cannot_Attack_Each_Other_In_The_Ensuing_Clash()
    {
        var e = CoalitionSetup(out var s);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.coalition", Amount = 1 });

        // First maneuver belongs to the instigator: Green is a legal target, the partner is not.
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        var attacks = e.LegalMoves().Where(m => m.Type == MoveType.Attack).ToList();
        Assert.Contains(attacks, m => m.TargetPlayerId == "p2");
        Assert.DoesNotContain(attacks, m => m.TargetPlayerId == "p1");
        Assert.Throws<InvalidOperationException>(() =>
            e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" }));
    }

    [Fact]
    public void Coalition_Partner_May_Decline_And_The_Clash_Still_Happens()
    {
        var e = CoalitionSetup(out var s);
        e.Apply(new Move { Type = MoveType.PassReaction, PlayerId = "p1" });

        Assert.Equal(0, s.Territories["T1"].ClansOf(ClanColor.Blue));
        Assert.NotNull(s.ActiveClash);
        Assert.Equal("T1", s.ActiveClash!.TerritoryId);
        Assert.Equal("p0", s.ActiveClash!.InstigatorId);
    }

    // ------------------------------------------------- The King and the Land

    [Fact]
    public void King_And_The_Land_Can_Trade_An_Advantage_For_An_Epic_Tale()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.EpicDeck.Add("epic.balors_eye");
        s.Players[0].Advantages.Add("advantage.plains");
        s.Players[0].Hand.Add("action.the_king_and_the_land");
        var e = SeasonEngine(s);

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.the_king_and_the_land",
            CardIds = new[] { "advantage.plains" },
        });

        Assert.Empty(s.Players[0].Advantages);
        Assert.Contains("epic.balors_eye", s.Players[0].Hand);
    }

    [Fact]
    public void King_And_The_Land_Can_Gift_An_Advantage_For_A_Deed()
    {
        var s = NewState(2);
        // The Plains advantage belongs to territory.plains — the helper's default definition.
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        s.Players[0].Advantages.Add("advantage.plains");
        s.Players[0].Hand.Add("action.the_king_and_the_land");
        var e = SeasonEngine(s);
        var blueReserve = s.Players[1].ClanReserve;

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.the_king_and_the_land",
            CardIds = new[] { "advantage.plains" }, TargetPlayerId = "p1",
        });

        Assert.Empty(s.Players[0].Advantages);
        Assert.Contains("advantage.plains", s.Players[1].Advantages);
        Assert.Equal(1, s.Players[0].Deeds);
        Assert.Equal(2, t0.ClansOf(ClanColor.Blue));            // recipient placed a clan
        Assert.Equal(blueReserve - 1, s.Players[1].ClanReserve);
    }

    // ----------------------------------------------------------- Clans Harmony

    [Fact]
    public void Clans_Harmony_Places_A_Clan_In_Every_Shared_Territory()
    {
        var s = NewState(2);
        var shared1 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        var shared2 = AddTerritory(s, "T1", (ClanColor.Red, 2), (ClanColor.Blue, 1));
        var solo = AddTerritory(s, "T2", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.clans_harmony");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.clans_harmony" });

        Assert.Equal(2, shared1.ClansOf(ClanColor.Red));
        Assert.Equal(3, shared2.ClansOf(ClanColor.Red));
        Assert.Equal(1, solo.ClansOf(ClanColor.Red)); // not shared — untouched
    }

    [Fact]
    public void Clans_Harmony_Alternate_Mode_Places_One_Clan_Anywhere_Present()
    {
        var s = NewState(2);
        var solo = AddTerritory(s, "T0", (ClanColor.Red, 1));
        AddTerritory(s, "T1", (ClanColor.Blue, 1));
        s.Players[0].Hand.Add("action.clans_harmony");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.clans_harmony", TerritoryId = "T0" });

        Assert.Equal(2, solo.ClansOf(ClanColor.Red));
    }
}
