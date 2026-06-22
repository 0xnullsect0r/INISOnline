using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

public class ClashTests
{
    /// <summary>Two adjacent territories; Red moves into a territory holding Blue via Conquest.</summary>
    private static GameEngine ConquestSetup(out GameState s)
    {
        s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));   // contested destination
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));    // Red's source (adjacent)
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest",
            FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2,
        });
        return engine;
    }

    [Fact]
    public void Moving_Into_An_Occupied_Territory_Starts_A_Clash()
    {
        var engine = ConquestSetup(out _);
        Assert.Equal(GamePhase.Clash, engine.State.Phase);
        Assert.NotNull(engine.State.ActiveClash);
        Assert.Equal("p0", engine.State.ActiveClash!.InstigatorId);
    }

    [Fact]
    public void Defender_May_Shelter_A_Clan_In_A_Citadel_Protecting_It_From_Attack()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        t0.Citadels = 1;                       // one shelter available
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // Blue's citadel step: shelter the lone clan, then decline further.
        Assert.Equal(PendingKind.ClashShelter, engine.Pending!.Kind);
        Assert.Equal("p1", engine.Pending!.PlayerId);
        engine.Apply(new Move { Type = MoveType.ClashShelter, PlayerId = "p1" });

        // Resolution: Red is the only one with exposed clans (Blue's clan is protected).
        Assert.Equal(PendingKind.ClashManeuver, engine.Pending!.Kind);
        Assert.Equal("p0", engine.Pending!.PlayerId);
        Assert.Equal(1, engine.State.ActiveClash!.Sheltered[ClanColor.Blue]);
    }

    [Fact]
    public void Attack_Removes_An_Exposed_Clan_When_Defender_Has_No_Action_Cards()
    {
        var engine = ConquestSetup(out var s);
        // No citadels -> straight to resolution; Red (instigator) attacks Blue.
        Assert.Equal(PendingKind.ClashManeuver, engine.Pending!.Kind);
        engine.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        // Blue has no Action cards -> the clan is removed automatically.
        Assert.Equal(0, s.Territories["T0"].ClansOf(ClanColor.Blue));
        // Only Red (the instigator) now has exposed clans, so it must choose to end the clash.
        Assert.Equal(PendingKind.ClashManeuver, engine.Pending!.Kind);
        Assert.Equal("p0", engine.Pending!.PlayerId);
        engine.Apply(new Move { Type = MoveType.EndClash, PlayerId = "p0" });
        Assert.Equal(GamePhase.Season, engine.State.Phase);
        Assert.Null(engine.State.ActiveClash);
    }

    [Fact]
    public void Attacked_Player_May_Discard_An_Action_Card_Instead_Of_Losing_A_Clan()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("action.bard"); // a card Blue can sacrifice
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 1 });

        engine.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(PendingKind.AttackResponse, engine.Pending!.Kind);
        engine.Apply(new Move { Type = MoveType.AttackDiscardCard, PlayerId = "p1", CardId = "action.bard" });

        Assert.Equal(2, s.Territories["T0"].ClansOf(ClanColor.Blue)); // no clan lost
        Assert.DoesNotContain("action.bard", s.Players[1].Hand);
    }

    [Fact]
    public void Withdraw_Moves_Exposed_Clans_To_An_Adjacent_Led_Territory()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 5));   // Blue will be instigator-attacked? set Red as instigator
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 2));
        var t2 = AddTerritory(s, "T2", (ClanColor.Red, 4));    // Red is chieftain here -> a valid withdraw target
        Link(t0, t1);
        Link(t0, t2);
        s.Players[0].Hand.Add("action.conquest");
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        // Red moves 2 from T1 into T0 (Blue present) -> clash, Red instigator with 2 exposed.
        engine.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        engine.Apply(new Move { Type = MoveType.Withdraw, PlayerId = "p0", ToTerritoryId = "T2" });
        Assert.Equal(0, s.Territories["T0"].ClansOf(ClanColor.Red)); // Red left the contested tile
        Assert.Equal(6, s.Territories["T2"].ClansOf(ClanColor.Red)); // 4 + 2 withdrawn
    }

    [Fact]
    public void Festival_Costs_The_Clash_Initiator_A_Clan_Before_The_Citadels_Step()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        t0.HasFestival = true;
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        var engine = SeasonEngine(s, brenn: 0, opened: true);
        engine.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });
        // Red moved 2 into T0 (total 2) then immediately lost one to the Festival.
        Assert.Equal(1, s.Territories["T0"].ClansOf(ClanColor.Red));
    }
}
