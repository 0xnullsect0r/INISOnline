using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

namespace Inis.Core.Effects;

/// <summary>Signature of a card-effect handler. Reads targets from the move and mutates via the engine.</summary>
public delegate void EffectHandler(GameEngine engine, PlayerState player, CardDefinition card, Move move);

/// <summary>
/// Maps every card definition to exactly one effect handler. Cards whose precise effect is not
/// yet modeled resolve as a legal no-op (the rules explicitly allow playing a card for no/partial
/// effect). Handlers are keyed by <see cref="CardDefinition.ResolvedEffectId"/> so the expansion's
/// updated Exploration/Druid reuse the base handlers.
/// </summary>
public static class EffectRegistry
{
    private static readonly Dictionary<string, EffectHandler> Handlers = Build();

    /// <summary>Resolves a card's effect. Every card has a handler; unmodeled ones are a no-op.</summary>
    public static void Resolve(GameEngine engine, PlayerState player, CardDefinition card, Move move)
    {
        if (Handlers.TryGetValue(card.ResolvedEffectId, out var handler)) handler(engine, player, card, move);
        else NoOp(engine, player, card, move);
    }

    /// <summary>True if a handler is registered for the given effect id (every base card has one).</summary>
    public static bool HasHandler(string effectId) => Handlers.ContainsKey(effectId);

    private static Dictionary<string, EffectHandler> Build()
    {
        var map = new Dictionary<string, EffectHandler>
        {
            // ----- Action cards -----
            ["action.new_clans"] = NewClans,
            ["action.sanctuary"] = Sanctuary,
            ["action.citadel"] = Citadel,
            ["action.craftsmen_peasants"] = CraftsmenPeasants,
            ["action.conquest"] = Conquest,
            ["action.migration"] = Migration,
            ["action.bard"] = Bard,
            ["action.druid"] = Druid,
            ["action.festival"] = Festival,
            ["action.exploration"] = Exploration,
            ["action.new_alliance"] = NewAlliance,
            ["action.scouts_spies"] = ScoutsAndSpies,
            ["action.warlord"] = Warlord,
            ["action.master_craftsman"] = MasterCraftsman,
            ["action.emissaries"] = Emissaries,
            ["action.clans_harmony"] = ClansHarmony,
            // Geis / Coalition / The King and the Land / Fili / Raid: reactive or unmodeled -> no-op.

            // ----- Epic Tales -----
            ["epic.balors_eye"] = BalorsEye,
            ["epic.stone_of_fal"] = StoneOfFal,
            ["epic.deirdres_beauty"] = DeirdresBeauty,
            ["epic.battle_of_moytura"] = BattleOfMoytura,
            ["epic.the_morrigan"] = TheMorrigan,
        };

        // Guarantee one handler per card definition: fill any gaps with the legal no-op.
        foreach (var c in GameData.Default.Cards)
            if (!map.ContainsKey(c.ResolvedEffectId))
                map[c.ResolvedEffectId] = NoOp;

        return map;
    }

    // ----------------------------------------------------------------- Action effects

    private static void NewClans(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color)) return;
        e.PlaceClans(p, t, m.Amount > 0 ? m.Amount : 2);
    }

    private static void Sanctuary(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color)) return;
        e.BuildSanctuary(t);
        e.DrawEpic(p);
    }

    private static void Citadel(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color)) return;
        e.BuildCitadel(t);
        var adv = m.CardIds?.FirstOrDefault();
        if (adv is not null) e.TakeAdvantage(p, adv);
    }

    private static void CraftsmenPeasants(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        foreach (var t in e.State.Territories.Values.ToList())
            if (t.IsPresent(p.Color) && t.TotalCitadels > 0)
                e.PlaceClans(p, t, t.TotalCitadels);
    }

    private static void Conquest(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var from = e.Territory(m.FromTerritoryId);
        var to = e.Territory(m.ToTerritoryId ?? m.TerritoryId);
        if (from is null || to is null || !to.Adjacent.Contains(from.InstanceId)) return;
        e.MoveClans(p, from, to, m.Amount > 0 ? m.Amount : from.ClansOf(p.Color));
    }

    private static void Migration(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var from = e.Territory(m.FromTerritoryId ?? m.TerritoryId);
        var to = e.Territory(m.ToTerritoryId);
        if (from is null || to is null || !from.Adjacent.Contains(to.InstanceId)) return;
        e.MoveClans(p, from, to, m.Amount > 0 ? m.Amount : from.ClansOf(p.Color));
    }

    private static void Bard(GameEngine e, PlayerState p, CardDefinition c, Move m) => e.DrawEpic(p);

    private static void Druid(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var pick = m.CardIds?.FirstOrDefault() ?? e.State.ActionDiscard.LastOrDefault();
        if (pick is null || !e.State.ActionDiscard.Remove(pick)) return;
        p.Hand.Add(pick);
    }

    private static void Festival(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color) || t.Sanctuaries == 0) return;
        t.HasFestival = true;
        e.PlaceClans(p, t, 1);
    }

    private static void Exploration(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var anchor = e.Territory(m.TerritoryId) ?? e.State.Territories.Values.FirstOrDefault();
        if (anchor is null) return;
        var inPlay = e.State.Territories.Values.Select(t => t.DefinitionId).ToHashSet();
        var nextDef = GameData.Default.Territories.FirstOrDefault(d => !inPlay.Contains(d.Id));
        if (nextDef is null) return;

        var inst = new TerritoryState { InstanceId = $"T{e.State.Territories.Count}", DefinitionId = nextDef.Id };
        e.State.Territories[inst.InstanceId] = inst;
        inst.Adjacent.Add(anchor.InstanceId);
        anchor.Adjacent.Add(inst.InstanceId);
        // Touch a second existing territory to satisfy the "adjacent to two" rule when possible.
        var second = e.State.Territories.Values.FirstOrDefault(t => t != inst && t != anchor);
        if (second is not null) { inst.Adjacent.Add(second.InstanceId); second.Adjacent.Add(inst.InstanceId); }
        e.PlaceClans(p, inst, 1);
    }

    private static void NewAlliance(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        var victim = m.TargetColor;
        if (t is null || victim is null || victim == p.Color) return;
        if (t.ClansOf(victim.Value) <= 0 || p.ClanReserve <= 0) return;
        e.RemoveClan(t, victim.Value);   // remove an opponent clan...
        e.PlaceClans(p, t, 1);           // ...and replace it with one of yours (no clash from placing).
    }

    private static void ScoutsAndSpies(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        // The "look at a hand" part is information only; the optional move is modeled.
        var from = e.Territory(m.FromTerritoryId);
        var to = e.Territory(m.ToTerritoryId);
        if (from is not null && to is not null && from.Adjacent.Contains(to.InstanceId))
            e.MoveClans(p, from, to, m.Amount > 0 ? m.Amount : 1);
    }

    private static void Warlord(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null) return;
        if (p.ClanReserve > 0) e.PlaceClans(p, t, 1);
        e.StartClash(t.InstanceId, p.PlayerId);
    }

    private static void MasterCraftsman(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var discard = m.CardIds?.FirstOrDefault();
        if (discard is not null && p.Hand.Remove(discard)) e.State.ActionDiscard.Add(discard);
        e.DrawEpic(p);
    }

    private static void Emissaries(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var from = e.Territory(m.FromTerritoryId);
        var to = e.Territory(m.ToTerritoryId);
        if (from is null || to is null || !from.Adjacent.Contains(to.InstanceId)) return;
        // Emissaries move does not start a clash, so place rather than move into contention.
        var n = Math.Min(1, from.ClansOf(p.Color));
        if (n <= 0) return;
        from.AddClans(p.Color, -n);
        to.AddClans(p.Color, n);
    }

    private static void ClansHarmony(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color)) return;
        // Shared territory: more than one player present.
        if (t.Clans.Count(kv => kv.Value > 0) < 2) return;
        e.PlaceClans(p, t, m.Amount > 0 ? m.Amount : 1);
    }

    // ----------------------------------------------------------------- Epic Tale effects

    private static void BalorsEye(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || m.TargetColor is null) return;
        e.RemoveClan(t, m.TargetColor.Value);
    }

    private static void StoneOfFal(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null || !t.IsPresent(p.Color)) return;
        e.PlaceClans(p, t, 2);
    }

    private static void DeirdresBeauty(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        e.DrawEpic(p);
        var t = e.Territory(m.TerritoryId);
        if (t is not null && t.IsPresent(p.Color)) e.RemoveClan(t, p.Color);
    }

    private static void BattleOfMoytura(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var to = e.Territory(m.ToTerritoryId ?? m.TerritoryId);
        var from = e.Territory(m.FromTerritoryId);
        if (to is null) return;
        if (from is not null && from.Adjacent.Contains(to.InstanceId))
            e.MoveClans(p, from, to, m.Amount > 0 ? m.Amount : 1);
        else if (t_present(to, p)) e.PlaceClans(p, to, m.Amount > 0 ? m.Amount : 1);

        static bool t_present(TerritoryState t, PlayerState pl) => t.IsPresent(pl.Color);
    }

    private static void TheMorrigan(GameEngine e, PlayerState p, CardDefinition c, Move m)
    {
        var t = e.Territory(m.TerritoryId);
        if (t is null) return;
        var instigator = m.TargetPlayerId ?? p.PlayerId;
        e.StartClash(t.InstanceId, instigator);
    }

    // ----------------------------------------------------------------- fallback

    private static void NoOp(GameEngine e, PlayerState p, CardDefinition c, Move m) { /* legal: played for no effect */ }
}
