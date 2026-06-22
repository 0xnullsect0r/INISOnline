using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

namespace Inis.Core.Ai;

/// <summary>
/// A deterministic heuristic AI. Given an engine waiting on a decision, it returns a
/// single, fully-targeted <see cref="Move"/> that the engine can apply. It drives the
/// same public API a human client uses (<see cref="GameEngine.LegalMoves"/> /
/// <see cref="GameEngine.Apply"/>), so it doubles as: single-player opponents, server
/// seat-fill, and an AI-vs-AI soak harness for the rules engine.
///
/// Determinism: choices are made by stable ordering only (no RNG), so a game seed plus
/// AI-controlled seats reproduces an identical game — see the determinism tests.
/// </summary>
public static class HeuristicAi
{
    // Rough "play this if you can make it do something" ordering, by ResolvedEffectId.
    private static readonly string[] CardPriority =
    {
        "action.stone_of_fal",        // (epic) place 2 — strong board growth
        "action.new_clans",           // grow presence (Land)
        "action.sanctuary",           // Religion + draws an Epic Tale
        "action.craftsmen_peasants",  // mass clan placement where you have Citadels
        "action.citadel",             // building + advantage
        "action.exploration",         // new territory (Land)
        "action.conquest",            // expand into a new territory
        "action.migration",           // reposition / expand
        "action.new_alliance",        // swap an opponent's clan for yours
        "action.bard",                // draw an Epic Tale
        "action.master_craftsman",    // cycle + draw
        "action.druid",               // recover an Action card
        "action.festival",            // protect a sanctuary territory
        "action.warlord",             // pick a fight where it helps
        "epic.balors_eye",            // remove an opponent clan
        "epic.deirdres_beauty",       // draw (at the cost of a clan) — low priority
        "epic.battle_of_moytura",
        "epic.the_morrigan",
    };

    /// <summary>Chooses a complete move for whichever player the engine is waiting on.</summary>
    public static Move ChooseMove(GameEngine e)
    {
        var pending = e.Pending ?? throw new InvalidOperationException("No pending decision for the AI.");
        var legal = e.LegalMoves();
        if (legal.Count == 0) throw new InvalidOperationException("No legal moves for the AI.");
        var me = e.State.PlayerById(pending.PlayerId)!;

        return pending.Kind switch
        {
            PendingKind.Draft         => DraftPick(e, me, legal),
            PendingKind.SeasonTurn    => SeasonMove(e, me, legal),
            PendingKind.ClashShelter  => Prefer(legal, MoveType.ClashShelter),       // always take cover
            PendingKind.ClashManeuver => legal.FirstOrDefault(m => m.Type == MoveType.Attack)
                                          ?? Prefer(legal, MoveType.EndClash),        // hit, else stop
            PendingKind.AttackResponse => Prefer(legal, MoveType.AttackRemoveClan),   // absorb by losing a clan
            _ => legal[0],
        };
    }

    private static Move Prefer(IReadOnlyList<Move> legal, MoveType type)
        => legal.FirstOrDefault(m => m.Type == type) ?? legal[0];

    // ------------------------------------------------------------------- draft

    private static Move DraftPick(GameEngine e, PlayerState me, IReadOnlyList<Move> legal)
    {
        // Prefer cards by the same priority; otherwise take the first offered.
        foreach (var eff in CardPriority)
        {
            var pick = legal.FirstOrDefault(m =>
                m.CardId is { } c && e.Data.TryGetCard(c, out var d) && d.ResolvedEffectId == eff);
            if (pick is not null) return pick;
        }
        return legal[0];
    }

    // ------------------------------------------------------------------ season

    private static Move SeasonMove(GameEngine e, PlayerState me, IReadOnlyList<Move> legal)
    {
        // 1) If we can stake a claim to the throne, do it.
        var pretender = legal.FirstOrDefault(m => m.Type == MoveType.TakePretender);
        if (pretender is not null) return pretender;

        // 2) Play the highest-priority card we can actually make useful.
        foreach (var eff in CardPriority)
        {
            var cid = me.Hand.FirstOrDefault(c => e.Data.TryGetCard(c, out var d) && d.ResolvedEffectId == eff);
            if (cid is null) continue;
            var move = BuildCardMove(e, me, cid);
            if (move is not null) return move;
        }

        // 3) Otherwise pass if allowed.
        var pass = legal.FirstOrDefault(m => m.Type == MoveType.Pass);
        if (pass is not null) return pass;

        // 4) Forced to act (e.g. the Brenn must open): play any card we hold.
        var anyPlay = legal.FirstOrDefault(m => m.Type == MoveType.PlayCard);
        return anyPlay ?? legal[0];
    }

    /// <summary>
    /// Builds a fully-targeted PlayCard move for the given card, or null if the card cannot
    /// do anything useful right now (so the caller moves on to the next option).
    /// </summary>
    private static Move? BuildCardMove(GameEngine e, PlayerState me, string cardId)
    {
        var st = e.State;
        var present = st.Territories.Values.Where(t => t.IsPresent(me.Color)).ToList();
        if (!e.Data.TryGetCard(cardId, out var def)) return null;

        Move Play(string? terr = null, string? from = null, string? to = null,
                  ClanColor? targetColor = null, int amount = 0) => new()
        {
            Type = MoveType.PlayCard, PlayerId = me.PlayerId, CardId = cardId,
            TerritoryId = terr, FromTerritoryId = from, ToTerritoryId = to,
            TargetColor = targetColor, Amount = amount,
        };

        switch (def.ResolvedEffectId)
        {
            case "action.new_clans":
            {
                if (me.ClanReserve <= 0 || present.Count == 0) return null;
                var t = present.OrderBy(x => x.ClansOf(me.Color)).First();
                return Play(t.InstanceId, amount: Math.Min(2, me.ClanReserve));
            }
            case "epic.stone_of_fal":
            {
                if (me.ClanReserve <= 0 || present.Count == 0) return null;
                return Play(present[0].InstanceId, amount: Math.Min(2, me.ClanReserve));
            }
            case "action.sanctuary":
                return present.Count > 0 && st.SanctuariesRemaining > 0 ? Play(present[0].InstanceId) : null;
            case "action.citadel":
                return present.Count > 0 && st.CitadelsRemaining > 0 ? Play(present[0].InstanceId) : null;
            case "action.craftsmen_peasants":
                return me.ClanReserve > 0 && present.Any(t => t.TotalCitadels > 0) ? Play() : null;
            case "action.festival":
            {
                var t = present.FirstOrDefault(x => x.Sanctuaries > 0);
                return t is not null && me.ClanReserve > 0 ? Play(t.InstanceId) : null;
            }
            case "action.exploration":
                return present.Count > 0 && me.ClanReserve > 0 ? Play(present[0].InstanceId) : null;

            case "action.conquest":
            case "action.migration":
            {
                // Expand: move a single clan from a territory we can spare one from into an
                // adjacent territory we are NOT yet present in (gains a new Land point),
                // falling back to any adjacent territory.
                foreach (var f in present.Where(x => x.ClansOf(me.Color) >= 2))
                {
                    var fresh = f.Adjacent.Select(id => st.Territories[id])
                        .FirstOrDefault(t => !t.IsPresent(me.Color));
                    var dest = fresh ?? f.Adjacent.Select(id => st.Territories[id]).FirstOrDefault();
                    if (dest is not null) return Play(from: f.InstanceId, to: dest.InstanceId, amount: 1);
                }
                return null;
            }

            case "action.new_alliance":
            {
                if (me.ClanReserve <= 0) return null;
                foreach (var t in st.Territories.Values)
                {
                    var victim = t.Clans.Keys.FirstOrDefault(c => c != me.Color && t.ClansOf(c) > 0);
                    if (t.Clans.ContainsKey(victim) && victim != me.Color && t.ClansOf(victim) > 0)
                        return Play(t.InstanceId, targetColor: victim);
                }
                return null;
            }

            case "epic.balors_eye":
            {
                foreach (var t in st.Territories.Values)
                {
                    var victim = t.Clans.Keys.FirstOrDefault(c => c != me.Color && t.ClansOf(c) > 0);
                    if (victim != me.Color && t.ClansOf(victim) > 0) return Play(t.InstanceId, targetColor: victim);
                }
                return null;
            }

            case "action.warlord":
            case "epic.the_morrigan":
            {
                // Only worth it where we can actually contest opponents.
                var t = present.FirstOrDefault(x => x.Clans.Any(kv => kv.Key != me.Color && kv.Value > 0))
                        ?? st.Territories.Values.FirstOrDefault(x => x.Clans.Any(kv => kv.Key != me.Color && kv.Value > 0));
                return t is not null ? Play(t.InstanceId) : null;
            }

            case "epic.deirdres_beauty":
            case "epic.battle_of_moytura":
                return present.Count > 0 ? Play(present[0].InstanceId) : null;

            // Pure draw / cycle effects — always fine to play.
            case "action.bard":
            case "action.master_craftsman":
                return Play();
            case "action.druid":
                return st.ActionDiscard.Count > 0 ? Play() : null;

            default:
                return null; // unknown/reactive card: let the caller try something else
        }
    }
}
