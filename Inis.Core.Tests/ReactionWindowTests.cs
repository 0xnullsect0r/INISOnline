using Inis.Core.Ai;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

/// <summary>
/// Reaction (Triskel) windows: Geis, Lug Samildanach, Master Craftsman, Warlord, Bard,
/// Raid, Lug's Spear, and the maneuver-Triskels (Tale of Cúchulain, Ogma's Eloquence).
/// </summary>
public class ReactionWindowTests
{
    // ------------------------------------------------------------------ Geis

    [Fact]
    public void No_Window_Opens_When_No_Opponent_Holds_A_Triskel()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });

        // Resolved inline: clans placed, no reaction prompt, turn advanced.
        Assert.Equal(3, s.Territories["T0"].ClansOf(ClanColor.Red));
        Assert.Empty(s.ReactionStack);
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
    }

    [Fact]
    public void Geis_Cancels_An_Action_Card_Before_Its_Effect_Resolves()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        s.Players[1].Hand.Add("action.geis");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });

        // The play is interrupted; the opponent is prompted with the trigger card visible.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        Assert.Equal("action.new_clans", e.Pending!.CardId);
        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.PlayReaction && m.CardId == "action.geis");
        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.PassReaction);

        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.geis" });

        // Effect never resolved; both cards discarded; turn continues.
        Assert.Equal(1, s.Territories["T0"].ClansOf(ClanColor.Red));
        Assert.Contains("action.new_clans", s.ActionDiscard);
        Assert.Contains("action.geis", s.ActionDiscard);
        Assert.Empty(s.ReactionStack);
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
    }

    [Fact]
    public void Passing_The_Geis_Window_Lets_The_Card_Resolve_Normally()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        s.Players[1].Hand.Add("action.geis");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });
        e.Apply(new Move { Type = MoveType.PassReaction, PlayerId = "p1" });

        Assert.Equal(3, s.Territories["T0"].ClansOf(ClanColor.Red));
        Assert.Contains("action.geis", s.Players[1].Hand); // kept for later
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
    }

    [Fact]
    public void Lug_Samildanach_Keeps_A_Card_Cancelled_By_Geis()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        s.Players[0].Hand.Add("epic.lug_samildanach");
        s.Players[1].Hand.Add("action.geis");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.geis" });

        // The cancel opens a nested window for the victim.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.lug_samildanach" });

        // The cancelled card returns to hand (unresolved); Lug Samildanach is spent.
        Assert.Contains("action.new_clans", s.Players[0].Hand);
        Assert.DoesNotContain("action.new_clans", s.ActionDiscard);
        Assert.Contains("epic.lug_samildanach", s.EpicDiscard);
        Assert.Equal(1, s.Territories["T0"].ClansOf(ClanColor.Red)); // effect stayed cancelled
        Assert.Empty(s.ReactionStack);
        Assert.Equal(PendingKind.SeasonTurn, e.Pending!.Kind);
    }

    // ------------------------------------------------------- Master Craftsman

    [Fact]
    public void Master_Craftsman_Passes_A_Played_Epic_On_And_Gains_A_Deed()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 2));
        s.Players[0].Hand.Add("epic.battle_of_moytura");
        s.Players[0].Hand.Add("action.master_craftsman");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.battle_of_moytura", TerritoryId = "T0" });

        // After the epic resolves, its player may hand it on instead of discarding it.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "action.master_craftsman", TargetPlayerId = "p1" });

        Assert.Contains("epic.battle_of_moytura", s.Players[1].Hand);
        Assert.DoesNotContain("epic.battle_of_moytura", s.EpicDiscard);
        Assert.Contains("action.master_craftsman", s.ActionDiscard);
        Assert.Equal(1, s.Players[0].Deeds);
    }

    // --------------------------------------------------------------- Warlord

    [Fact]
    public void Warlord_Joins_A_Starting_Clash_And_Chooses_The_First_Maneuver()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("action.warlord");
        var e = SeasonEngine(s);

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // The clash pauses before its Citadels step for Warlord holders.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
        var blueReserve = s.Players[1].ClanReserve;
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.warlord", TargetPlayerId = "p1" });

        Assert.Equal(2, t0.ClansOf(ClanColor.Blue));            // joined with one clan
        Assert.Equal(blueReserve - 1, s.Players[1].ClanReserve);
        // Blue chose itself to act first in the Resolution step.
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);
        Assert.Equal("p1", e.Pending!.PlayerId);
    }

    // ------------------------------------------------------------ Bard / Raid

    [Fact]
    public void Bard_Reaction_Awards_A_Deed_After_An_Attack_Removes_A_Clan()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("action.bard");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });

        // Blue had no Action cards, so the clan was removed at once — Bard may fire.
        Assert.Equal(0, t0.ClansOf(ClanColor.Blue));
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "action.bard" });

        Assert.Equal(1, s.Players[0].Deeds);
        Assert.Contains("action.bard", s.ActionDiscard);
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);
    }

    [Fact]
    public void Raid_Steals_An_Action_Card_From_The_Attacked_Player()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("action.raid");
        s.Players[1].Hand.Add("action.sanctuary");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 1 });

        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        e.Apply(new Move { Type = MoveType.AttackRemoveClan, PlayerId = "p1" });

        // The attack fully resolved; the attacker may raid the defender's hand.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "action.raid" });

        Assert.Contains("action.sanctuary", s.Players[0].Hand);
        Assert.DoesNotContain("action.sanctuary", s.Players[1].Hand);
        Assert.Contains("action.raid", s.ActionDiscard);
    }

    // ------------------------------------------------------------ Lug's Spear

    [Fact]
    public void Lugs_Spear_Suppresses_All_Triskels_For_The_Rest_Of_The_Clash()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("epic.lugs_spear");
        s.Players[0].Hand.Add("action.bard");
        s.Players[1].Hand.Add("action.warlord");
        s.Players[1].Hand.Add("epic.ogmas_eloquence");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // Clash-start window: the instigator throws the Spear before Blue's Warlord can act.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.lugs_spear" });

        Assert.True(s.ActiveClash!.TriskelsBlocked);
        Assert.Contains("action.warlord", s.Players[1].Hand); // never got the chance
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);

        // Maneuver-Triskels are no longer offered, and post-attack windows stay shut.
        e.Apply(new Move { Type = MoveType.Attack, PlayerId = "p0", TargetPlayerId = "p1" });
        Assert.Equal(PendingKind.AttackResponse, e.Pending!.Kind); // Blue holds Action cards
        e.Apply(new Move { Type = MoveType.AttackRemoveClan, PlayerId = "p1" });
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind); // no Bard window despite holding Bard
        Assert.DoesNotContain(e.LegalMoves(), m => m.Type == MoveType.PlayCard);
    }

    // ------------------------------------------------------ maneuver-Triskels

    [Fact]
    public void Ogmas_Eloquence_Immediately_Ends_The_Clash_As_A_Maneuver()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("epic.ogmas_eloquence");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 1 });

        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind);
        Assert.Contains(e.LegalMoves(), m => m.Type == MoveType.PlayCard && m.CardId == "epic.ogmas_eloquence");
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.ogmas_eloquence" });

        Assert.Null(s.ActiveClash);
        Assert.Equal(GamePhase.Season, s.Phase);
        Assert.Contains("epic.ogmas_eloquence", s.EpicDiscard);
    }

    [Fact]
    public void Tale_Of_Cuchulain_Removes_Up_To_Two_Exposed_Clans()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[0].Hand.Add("epic.tale_of_cuchulain");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 1 });

        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "epic.tale_of_cuchulain", TargetColor = ClanColor.Blue });

        Assert.Equal(0, t0.ClansOf(ClanColor.Blue));
        Assert.Contains("epic.tale_of_cuchulain", s.EpicDiscard);
        Assert.Equal(PendingKind.ClashManeuver, e.Pending!.Kind); // Red still exposed
    }

    // ----------------------------------------------------------- AI behaviour

    [Fact]
    public void Ai_Geises_High_Value_Cards_And_Passes_On_Low_Value_Ones()
    {
        // High-value trigger: New Clans is near the top of the AI's priority list.
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        s.Players[1].Hand.Add("action.geis");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });
        var choice = HeuristicAi.ChooseMove(e);
        Assert.Equal(MoveType.PlayReaction, choice.Type);
        Assert.Equal("action.geis", choice.CardId);

        // Low-value trigger: Druid is not worth a Geis.
        var s2 = NewState(2);
        AddTerritory(s2, "T0", (ClanColor.Red, 1));
        s2.ActionDiscard.Add("action.festival");
        s2.Players[0].Hand.Add("action.druid");
        s2.Players[1].Hand.Add("action.geis");
        var e2 = SeasonEngine(s2);
        e2.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.druid" });
        Assert.Equal(MoveType.PassReaction, HeuristicAi.ChooseMove(e2).Type);
    }

    [Fact]
    public void Ai_Never_Proactively_Spends_Lugs_Spear()
    {
        var s = NewState(2);
        var t0 = AddTerritory(s, "T0", (ClanColor.Blue, 1));
        var t1 = AddTerritory(s, "T1", (ClanColor.Red, 3));
        Link(t0, t1);
        s.Players[0].Hand.Add("action.conquest");
        s.Players[1].Hand.Add("action.warlord");
        s.Players[0].Hand.Add("epic.lugs_spear");
        var e = SeasonEngine(s);
        e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.conquest", FromTerritoryId = "T1", ToTerritoryId = "T0", Amount = 2 });

        // The instigator (holding only the Spear) is prompted first and should pass.
        Assert.Equal(PendingKind.Reaction, e.Pending!.Kind);
        Assert.Equal("p0", e.Pending!.PlayerId);
        Assert.Equal(MoveType.PassReaction, HeuristicAi.ChooseMove(e).Type);
    }

    // ---------------------------------------------------------- serialization

    [Fact]
    public void Mid_Window_State_Survives_Reload_And_Resumes_Identically()
    {
        GameEngine Build()
        {
            var s = NewState(2);
            AddTerritory(s, "T0", (ClanColor.Red, 1));
            s.Players[0].Hand.Add("action.new_clans");
            s.Players[0].Hand.Add("epic.lug_samildanach");
            s.Players[1].Hand.Add("action.geis");
            var e = SeasonEngine(s);
            e.Apply(new Move { Type = MoveType.PlayCard, PlayerId = "p0", CardId = "action.new_clans", TerritoryId = "T0", Amount = 2 });
            return e;
        }

        // Interrupt mid-window, serialize, reload into a fresh engine.
        var live = Build();
        var restored = new GameEngine(InisJson.DeserializeState(InisJson.SerializeState(live.State)));

        Assert.Equal(PendingKind.Reaction, restored.Pending!.Kind);
        Assert.Single(restored.State.ReactionStack);

        // Both engines finish the same sequence: Geis, then Lug Samildanach.
        foreach (var e in new[] { live, restored })
        {
            e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p1", CardId = "action.geis" });
            e.Apply(new Move { Type = MoveType.PlayReaction, PlayerId = "p0", CardId = "epic.lug_samildanach" });
        }
        Assert.Equal(InisJson.SerializeState(live.State), InisJson.SerializeState(restored.State));
        Assert.Equal(PendingKind.SeasonTurn, restored.Pending!.Kind);
    }
}
