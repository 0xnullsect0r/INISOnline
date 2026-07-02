using Inis.Core.Data;
using Inis.Core.Effects;
using Inis.Core.Model;
using Inis.Core.Moves;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

public class CardEffectTests
{
    [Fact]
    public void Every_Card_Definition_Has_Exactly_One_Registered_Handler()
    {
        foreach (var c in GameData.Default.Cards)
            Assert.True(EffectRegistry.HasHandler(c.ResolvedEffectId), $"No handler for {c.Id}");
    }

    [Fact]
    public void Advantage_Cards_Live_In_The_Face_Up_Zone_And_Are_Playable()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        var engine = SeasonEngine(s);
        var p = s.Players[0];

        engine.TakeAdvantage(p, "advantage.forest");
        Assert.Contains("advantage.forest", p.Advantages);
        Assert.DoesNotContain("advantage.forest", p.Hand);

        Assert.Contains(engine.LegalMoves(),
            m => m.Type == MoveType.PlayCard && m.CardId == "advantage.forest");

        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "advantage.forest" });
        Assert.DoesNotContain("advantage.forest", p.Advantages);
    }

    [Fact]
    public void Taking_An_Advantage_Removes_It_From_Its_Previous_Holder()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        var engine = SeasonEngine(s);
        s.Players[1].Advantages.Add("advantage.hills");

        engine.TakeAdvantage(s.Players[0], "advantage.hills");
        Assert.Contains("advantage.hills", s.Players[0].Advantages);
        Assert.DoesNotContain("advantage.hills", s.Players[1].Advantages);
    }

    [Fact]
    public void New_Clans_Places_Clans_Where_Present()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.new_clans");
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "action.new_clans", TerritoryId = "T0", Amount = 3 });
        Assert.Equal(4, s.Territories["T0"].ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Sanctuary_Builds_A_Sanctuary_And_Draws_An_Epic_Tale()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.EpicDeck.Add("epic.the_dagda");
        s.Players[0].Hand.Add("action.sanctuary");
        var before = s.SanctuariesRemaining;
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "action.sanctuary", TerritoryId = "T0" });
        Assert.Equal(1, s.Territories["T0"].Sanctuaries);
        Assert.Equal(before - 1, s.SanctuariesRemaining);
        Assert.Contains("epic.the_dagda", s.Players[0].Hand);
    }

    [Fact]
    public void Citadel_Builds_A_Citadel_From_The_Shared_Reserve()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("action.citadel");
        var before = s.CitadelsRemaining;
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "action.citadel", TerritoryId = "T0" });
        Assert.Equal(1, s.Territories["T0"].Citadels);
        Assert.Equal(before - 1, s.CitadelsRemaining);
    }

    [Fact]
    public void Craftsmen_And_Peasants_Places_One_Clan_Per_Citadel_Where_Present()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 1));
        t.Citadels = 2;
        s.Players[0].Hand.Add("action.craftsmen_peasants");
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "action.craftsmen_peasants", TerritoryId = "T0" });
        Assert.Equal(3, t.ClansOf(ClanColor.Red)); // 1 + 2 citadels
    }

    [Fact]
    public void Balors_Eye_Removes_A_Clan_From_Any_Territory()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Blue, 2));
        s.Players[0].Hand.Add("epic.balors_eye");
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "epic.balors_eye", TerritoryId = "T0", TargetColor = ClanColor.Blue });
        Assert.Equal(1, t.ClansOf(ClanColor.Blue));
    }

    [Fact]
    public void Stone_Of_Fal_Places_Two_Clans()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 1));
        s.Players[0].Hand.Add("epic.stone_of_fal");
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "epic.stone_of_fal", TerritoryId = "T0" });
        Assert.Equal(3, t.ClansOf(ClanColor.Red));
    }

    [Fact]
    public void New_Alliance_Replaces_An_Opponent_Clan_With_Your_Own()
    {
        var s = NewState(2);
        var t = AddTerritory(s, "T0", (ClanColor.Red, 1), (ClanColor.Blue, 2));
        s.Players[0].Hand.Add("action.new_alliance");
        var engine = SeasonEngine(s);
        engine.Apply(new Move { Type = MoveType.PlayCard, CardId = "action.new_alliance", TerritoryId = "T0", TargetColor = ClanColor.Blue });
        Assert.Equal(1, t.ClansOf(ClanColor.Blue)); // one removed
        Assert.Equal(2, t.ClansOf(ClanColor.Red));  // one added
    }

    /// <summary>Every card can be played without throwing (unmodeled cards are a legal no-op).</summary>
    [Fact]
    public void Playing_Any_Card_Resolves_Without_Error()
    {
        foreach (var card in GameData.Default.Cards.Where(c => c.Type != CardType.Reference))
        {
            var s = NewState(2);
            var t0 = AddTerritory(s, "T0", (ClanColor.Red, 2), (ClanColor.Blue, 1));
            var t1 = AddTerritory(s, "T1", (ClanColor.Red, 2));
            Link(t0, t1);
            s.EpicDeck.Add("epic.the_dagda");
            s.ActionDiscard.Add("action.bard");
            s.Players[0].Hand.Add(card.Id);
            var engine = SeasonEngine(s);

            var ex = Record.Exception(() => engine.Apply(new Move
            {
                Type = MoveType.PlayCard, CardId = card.Id,
                TerritoryId = "T0", FromTerritoryId = "T1", ToTerritoryId = "T0",
                TargetPlayerId = "p1", TargetColor = ClanColor.Blue, Amount = 1,
            }));
            Assert.True(ex is null, $"Card {card.Id} threw: {ex}");
        }
    }
}
