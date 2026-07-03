using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

/// <summary>
/// The Epic Tale cards implemented in M3d — season-play effects and the reactive
/// (Triskel) ones — plus the newly modeled Advantage card effects.
/// </summary>
public class EpicAndAdvantageCardTests
{
    // ------------------------------------------------------ season-play epics

    [Fact]
    public void Eriu_Places_Clans_On_Sanctuary_Territories_Up_To_Three()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1)); t0.Sanctuaries = 1;
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 1)); t1.Sanctuaries = 2;
        var t2 = AddTerritory(s, "T2", (ClanColor.Red, 1)); // no sanctuary
        s.Players[0].Hand.Add("epic.eriu");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.eriu" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Red));
        Assert.Equal(2, t1.ClansOf(ClanColor.Red));
        Assert.Equal(1, t2.ClansOf(ClanColor.Red)); // untouched
    }

    [Fact]
    public void Kernunos_Sanctuary_Adds_A_Clan_And_A_Sanctuary()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("epic.kernunos_sanctuary");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.kernunos_sanctuary", TerritoryId = "T0" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Red));
        Assert.Equal(1, t0.Sanctuaries);
    }

    [Fact]
    public void The_Otherworld_Places_Or_Removes_One_Clan_Per_Sanctuary()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 2));
        t0.Sanctuaries = 2;
        s.Players[0].Hand.Add("epic.the_otherworld");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.the_otherworld", TerritoryId = "T0", TargetColor = ClanColor.Blue });

        Assert.Equal(0, t0.ClansOf(ClanColor.Blue)); // two sanctuaries -> two removals
    }

    [Fact]
    public void Dagdas_Harp_Places_One_Clan_Per_Other_Epic_In_Hand()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("epic.dagdas_harp");
        s.Players[0].Hand.Add("epic.eriu");
        s.Players[0].Hand.Add("epic.balors_eye");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.dagdas_harp", TerritoryId = "T0" });

        Assert.Equal(3, t0.ClansOf(ClanColor.Red)); // 1 + two other epics
    }

    [Fact]
    public void Tuans_Memory_Draws_Three_And_Keeps_One()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.EpicDeck.AddRange(new[] { "epic.balors_eye", "epic.eriu", "epic.the_fianna" });
        s.Players[0].Hand.Add("epic.tuans_memory");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.tuans_memory", CardIds = new[] { "epic.eriu" } });

        Assert.Contains("epic.eriu", s.Players[0].Hand);
        Assert.DoesNotContain("epic.balors_eye", s.Players[0].Hand);
        Assert.Contains("epic.balors_eye", s.EpicDiscard);
        Assert.Contains("epic.the_fianna", s.EpicDiscard);
    }

    [Fact]
    public void Champions_Share_Claims_The_Set_Aside_Action_Card()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.SetAsideActionCard = "action.warlord";
        s.Players[0].Hand.Add("epic.champions_share");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.champions_share" });

        Assert.Contains("action.warlord", s.Players[0].Hand);
        Assert.Null(s.SetAsideActionCard);
    }

    [Fact]
    public void Tailtus_Land_Adds_A_New_Territory_Without_Clans()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        AddTerritory(s, "T1", (ClanColor.Blue, 1));
        s.Players[0].Hand.Add("epic.tailtus_land");
        var e = SeasonEngine(s);
        var before = s.Territories.Count;

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.tailtus_land", TerritoryId = "T0" });

        Assert.Equal(before + 1, s.Territories.Count);
        var added = s.Territories.Values.First(t => t.InstanceId != "T0" && t.InstanceId != "T1");
        Assert.Equal(0, added.Clans.Values.Sum());
    }

    [Fact]
    public void Breas_Tyranny_Pushes_An_Opposing_Clan_Out_Peacefully()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 1));
        Link(t0, t1);
        s.Players[0].Hand.Add("epic.breas_tyranny");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.breas_tyranny", TerritoryId = "T0", TargetColor = ClanColor.Blue, ToTerritoryId = "T1" });

        Assert.Equal(0, t0.ClansOf(ClanColor.Blue));
        Assert.Equal(1, t1.ClansOf(ClanColor.Blue));
        Assert.Null(s.ActiveClash); // never a clash, even into Red's territory
    }

    [Fact]
    public void Manannans_Horses_Carry_Clans_Anywhere_Without_A_Clash()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 3));
        var t1 = AddTerritory(s, "T1", (ClanColor.Blue, 1)); // NOT adjacent
        s.Players[0].Hand.Add("epic.manannans_horses");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.manannans_horses", FromTerritoryId = "T0", ToTerritoryId = "T1", Amount = 3 });

        Assert.Equal(0, t0.ClansOf(ClanColor.Red));
        Assert.Equal(3, t1.ClansOf(ClanColor.Red));
        Assert.Null(s.ActiveClash);
    }

    [Fact]
    public void Children_Of_Dana_Place_A_Clan_Where_You_Are_Not_Present()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Blue, 1));
        s.Players[0].Hand.Add("epic.children_of_dana");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.children_of_dana", TerritoryId = "T1" });

        Assert.Equal(1, t1.ClansOf(ClanColor.Red));
        Assert.Null(s.ActiveClash);
    }

    [Fact]
    public void Maeves_Wealth_Exchanges_Action_Cards_With_Every_Player()
    {
        var s = NewState(3);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("epic.maeves_wealth");
        s.Players[0].Hand.Add("action.festival");
        s.Players[1].Hand.Add("action.bard");
        s.Players[2].Hand.Add("action.druid");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.maeves_wealth" });

        // Took one from each opponent, gave one back to each: everyone still has one Action card.
        Assert.Single(s.Players[1].Hand);
        Assert.Single(s.Players[2].Hand);
        Assert.Single(s.Players[0].Hand); // festival + 2 taken - 2 returned
    }

    [Fact]
    public void Nuada_Silverhand_Reinforces_Led_Territories_Per_Opponent()
    {
        var s = NewState(3);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 3), (ClanColor.Blue, 1), (ClanColor.Green, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 2)); // led, but no opponents
        s.Players[0].Hand.Add("epic.nuada_silverhand");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.nuada_silverhand" });

        Assert.Equal(5, t0.ClansOf(ClanColor.Red)); // +1 per opponent present
        Assert.Equal(2, t1.ClansOf(ClanColor.Red)); // unchanged
    }

    // ---------------------------------------------------------- reactive epics

    [Fact]
    public void The_Dagda_Cancels_An_Opponents_Epic_Tale()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 2));
        s.Players[0].Hand.Add("epic.battle_of_moytura");
        s.Players[1].Hand.Add("epic.the_dagda");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.battle_of_moytura", TerritoryId = "T0" });

        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "epic.the_dagda" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Red)); // effect never resolved
        Assert.Contains("epic.battle_of_moytura", s.EpicDiscard);
        Assert.Contains("epic.the_dagda", s.EpicDiscard);
    }

    [Fact]
    public void Battle_Frenzy_Turns_Sheltered_Clans_Out_Of_The_Citadels()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        t0.Citadels = 1;
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("epic.battle_frenzy");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // Blue shelters its only clan; the Citadels step ends; Red unleashes the frenzy.
        e.Apply(new Move { Type = MoveType.ClashShelter, PlayerId = "p1" });
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.battle_frenzy" });

        Assert.Equal(0, s.ActiveClash!.ShelteredTotal); // exposed again
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);
        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.Attack && m.TargetPlayerId == "p1");
    }

    [Fact]
    public void Dagdas_Cauldron_Returns_Clans_Lost_In_The_Clash()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("epic.dagdas_cauldron");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(1, t0.ClansOf(ClanColor.Blue)); // one removed
        // Blue agrees to end; Red agrees; as the clash ends the cauldron may fire.
        e.Apply(new Move { Type = MoveType.EndClash, PlayerId = "p1" });
        e.Apply(new Move { Type = MoveType.EndClash, PlayerId = "p0" });

        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "epic.dagdas_cauldron" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Blue)); // the lost clan came back
        Assert.Null(s.ActiveClash);
        Assert.Equal(GamePhase.Season, s.Phase);
    }

    [Fact]
    public void Strengs_Resolve_Awards_A_Deed_After_An_Attack()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("epic.strengs_resolve");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.strengs_resolve" });

        Assert.Equal(1, s.Players[0].Deeds);
        Assert.Contains("epic.strengs_resolve", s.EpicDiscard);
    }

    [Fact]
    public void Dagdas_Club_Saves_The_Clan_An_Attack_Removed()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("epic.dagdas_club");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(1, t0.ClansOf(ClanColor.Blue));
        // The defender is offered the club after the attacker's window slot.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "epic.dagdas_club" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Blue)); // not lost after all
        Assert.Contains("epic.dagdas_club", s.EpicDiscard);
    }

    [Fact]
    public void Diarmuid_And_Grainne_Relocate_The_Removed_Clan()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        var t2 = AddTerritory(s, "T2", (ClanColor.Blue, 1)); // escape destination
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("epic.diarmuid_grainne");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "epic.diarmuid_grainne", TerritoryId = "T2" });

        Assert.Equal(1, t0.ClansOf(ClanColor.Blue)); // still lost from the clash territory
        Assert.Equal(2, t2.ClansOf(ClanColor.Blue)); // ...but fled to T2
    }

    [Fact]
    public void Oengus_Ploy_Seizes_The_Next_Turn()
    {
        var s = NewState(3);
        AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1), (ClanColor.Green, 1));
        s.Players[2].Hand.Add("epic.oengus_ploy");
        var e = SeasonEngine(s);

        e.Apply(Move.Pass("p0"));

        // p1 would be next, but p2 steals the turn.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p2", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p2", CardId = "epic.oengus_ploy" });

        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
        Assert.Equal("p2", e.Pending!.PlayerId);
        Assert.Contains("epic.oengus_ploy", s.EpicDiscard);
    }

    [Fact]
    public void Cathbads_Word_Chooses_The_Set_Aside_Card_At_The_Assembly()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 1));
        s.Players[0].Hand.Add("epic.cathbads_word");
        var e = SeasonEngine(s);

        // Both pass; the season ends and the next Assembly begins.
        e.Apply(Move.Pass("p0"));
        e.Apply(Move.Pass("p1"));

        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        Assert.Equal("AssemblySetAside", e.Pending!.Trigger);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.cathbads_word", CardIds = new[] { "action.warlord" } });

        Assert.Equal("action.warlord", s.SetAsideActionCard);
        Assert.Contains("epic.cathbads_word", s.EpicDiscard);
        Assert.Equal(PendingKind.Draft, e.Pending!.Kind);
        Assert.Null(s.StagedActionDeck); // cleaned up after the deal
    }

    [Fact]
    public void The_Fianna_March_Sheltered_And_Exposed_Clans_Out_Of_A_Clash()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        var t2 = AddTerritory(s, "T2");
        Link(t0, t1);
        Link(t0, t2);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("epic.the_fianna");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // Red maneuvers first and ends; Blue plays The Fianna to march away instead.
        e.Apply(new Move { Type = MoveType.EndClash, PlayerId = "p0" });
        Assert.Equal("p1", e.Pending!.PlayerId);
        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.PlayCard && m.CardId == "epic.the_fianna");
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p1", CardId = "epic.the_fianna", ToTerritoryId = "T2", Amount = 2 });

        Assert.Equal(0, t0.ClansOf(ClanColor.Blue));
        Assert.Equal(2, t2.ClansOf(ClanColor.Blue));
        Assert.Contains("epic.the_fianna", s.EpicDiscard);
    }

    // ------------------------------------------------------------- advantages

    [Fact]
    public void Valley_Advantage_Places_A_Clan()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Advantages.Add("advantage.valley");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "advantage.valley", TerritoryId = "T0" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Plains_Advantage_Moves_Clans_Out_Of_The_Plains()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Red, 2)); // helper default def is territory.plains
        var t1 = AddTerritory(s, "T1");
        Link(t0, t1);
        s.Players[0].Advantages.Add("advantage.plains");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "advantage.plains", ToTerritoryId = "T1", Amount = 2 });

        Assert.Equal(0, t0.ClansOf(ClanColor.Red));
        Assert.Equal(2, t1.ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Salt_Mine_Advantage_Trades_Action_Cards()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Advantages.Add("advantage.salt_mine");
        s.Players[0].Hand.Add("action.druid");
        s.Players[1].Hand.Add("action.bard");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "advantage.salt_mine", TargetPlayerId = "p1" });

        Assert.Contains("action.bard", s.Players[0].Hand);
        Assert.Contains("action.druid", s.Players[1].Hand);
    }

    [Fact]
    public void Misty_Lands_Advantage_Turns_Action_Cards_Into_One_Epic()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.EpicDeck.AddRange(new[] { "epic.eriu", "epic.balors_eye" });
        s.Players[0].Advantages.Add("advantage.misty_lands");
        s.Players[0].Hand.Add("action.druid");
        s.Players[0].Hand.Add("action.bard");
        var e = SeasonEngine(s);

        e.Apply(new Move
        {
            Type = MoveType.PlayCard, PlayerId = "p0", CardId = "advantage.misty_lands",
            CardIds = new[] { "action.druid", "action.bard" },
        });

        Assert.DoesNotContain("action.druid", s.Players[0].Hand);
        Assert.DoesNotContain("action.bard", s.Players[0].Hand);
        Assert.Single(s.Players[0].Hand); // exactly one epic kept
        Assert.Single(s.EpicDiscard);     // the other drawn epic discarded
    }
}
