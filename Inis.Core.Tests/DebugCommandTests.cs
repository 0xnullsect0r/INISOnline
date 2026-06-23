using Inis.Core.Debug;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;
using Xunit;
using static Inis.Core.Tests.EngineTestHelpers;

namespace Inis.Core.Tests;

public class DebugCommandTests
{
    private static GameEngine Engine()
    {
        var s = NewState(2);
        AddTerritory(s, "T0", (ClanColor.Red, 1));
        return SeasonEngine(s);
    }

    [Fact]
    public void Grant_Adds_A_Card_To_The_Players_Hand()
    {
        var engine = Engine();
        DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "grant", PlayerId = "p0", CardId = "epic.the_dagda",
        });
        Assert.Contains("epic.the_dagda", engine.State.Players[0].Hand);
    }

    [Fact]
    public void Remove_Takes_A_Card_From_The_Hand()
    {
        var engine = Engine();
        engine.State.Players[0].Hand.Add("action.bard");
        DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "remove", PlayerId = "p0", CardId = "action.bard",
        });
        Assert.DoesNotContain("action.bard", engine.State.Players[0].Hand);
    }

    [Fact]
    public void Set_Deeds_Overrides_The_Players_Deed_Count()
    {
        var engine = Engine();
        DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "set_deeds", PlayerId = "p0", Amount = 3,
        });
        Assert.Equal(3, engine.State.Players[0].Deeds);
    }

    [Fact]
    public void Spawn_Clan_Adds_Clans_To_A_Territory()
    {
        var engine = Engine();
        DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "spawn_clan", PlayerId = "p0", TerritoryId = "T0", Amount = 2,
        });
        Assert.Equal(3, engine.State.Territories["T0"].ClansOf(ClanColor.Red));
    }

    [Fact]
    public void Granting_An_Unknown_Card_Is_Rejected()
    {
        var engine = Engine();
        Assert.Throws<InvalidOperationException>(() => DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "grant", PlayerId = "p0", CardId = "epic.not_a_real_card",
        }));
    }

    [Fact]
    public void Unknown_Command_Is_Rejected()
    {
        var engine = Engine();
        Assert.Throws<InvalidOperationException>(() => DebugCommandApi.Apply(engine, new Move
        {
            Type = MoveType.Debug, DebugCommand = "nuke_everything", PlayerId = "p0",
        }));
    }
}
