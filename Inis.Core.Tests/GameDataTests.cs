using Inis.Core.Data;
using Inis.Core.Model;
using Xunit;

namespace Inis.Core.Tests;

public class GameDataTests
{
    [Fact]
    public void Catalogue_Loads_From_Embedded_Resources()
    {
        var data = GameData.Default;
        Assert.NotEmpty(data.Cards);
        Assert.NotEmpty(data.Territories);
    }

    [Fact]
    public void Every_Card_Has_Unique_Id()
    {
        var ids = GameData.Default.Cards.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Base_Game_Composition_Totals_Are_Correct()
    {
        var data = GameData.Default;
        int Total(CardType t) => data.Cards.Where(c => c.Type == t).Sum(c => c.Count);

        // Base game: 16 territories/advantages; Seasons of Inis adds 6 more of each.
        Assert.Equal(16, data.Territories.Count(t => t.Expansion is null));
        Assert.Equal(22, data.Territories.Count);
        Assert.Equal(23, Total(CardType.Action));
        Assert.Equal(16, data.Cards.Count(c => c.Type == CardType.Advantage && c.Expansion is null));
        Assert.Equal(22, Total(CardType.Advantage));
        Assert.Equal(30, Total(CardType.EpicTale));
    }

    [Fact]
    public void Every_Territory_Has_A_Matching_Advantage_Card()
    {
        var data = GameData.Default;
        var advTerritoryIds = data.Cards
            .Where(c => c.Type == CardType.Advantage)
            .Select(c => c.TerritoryId).ToHashSet();
        foreach (var t in data.Territories)
            Assert.Contains(t.Id, advTerritoryIds);
    }

    [Fact]
    public void Advantage_Cards_Reference_Existing_Territories()
    {
        var data = GameData.Default;
        var territoryIds = data.Territories.Select(t => t.Id).ToHashSet();
        foreach (var adv in data.Cards.Where(c => c.Type == CardType.Advantage))
        {
            Assert.NotNull(adv.TerritoryId);
            Assert.Contains(adv.TerritoryId!, territoryIds);
        }
    }
}
