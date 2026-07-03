using Inis.Core.Model;
using Inis.Core.Moves;

namespace Inis.Core.Rules;

/// <summary>
/// Reaction (Triskel) windows. A trigger point calls <see cref="TryOpenReactionWindow"/>;
/// if any player holds a matching card, a <see cref="ReactionFrame"/> is pushed and the engine
/// prompts <see cref="PendingKind.Reaction"/>. Passing advances the queue; playing executes the
/// card. When the window closes, the frame's continuation resumes the interrupted flow. A window
/// only opens when at least one eligible holder exists, so games without the relevant cards are
/// entirely unaffected — and <see cref="MoveType.PassReaction"/> is always legal, so no player
/// (human or AI) can ever be stuck.
/// </summary>
public sealed partial class GameEngine
{
    // Card ids with reactive behaviour.
    private const string Geis = "action.geis";
    private const string LugSamildanach = "epic.lug_samildanach";
    private const string MasterCraftsman = "action.master_craftsman";
    private const string Warlord = "action.warlord";
    private const string BardCard = "action.bard";
    private const string RaidCard = "action.raid";
    private const string LugsSpear = "epic.lugs_spear";
    private const string TaleOfCuchulain = "epic.tale_of_cuchulain";
    private const string OgmasEloquence = "epic.ogmas_eloquence";
    private const string CoalitionCard = "action.coalition";
    private const string TheDagda = "epic.the_dagda";
    private const string BattleFrenzy = "epic.battle_frenzy";
    private const string DagdasCauldron = "epic.dagdas_cauldron";
    private const string DagdasClub = "epic.dagdas_club";
    private const string DiarmuidGrainne = "epic.diarmuid_grainne";
    private const string StrengsResolve = "epic.strengs_resolve";
    private const string OengusPloy = "epic.oengus_ploy";
    private const string CathbadsWord = "epic.cathbads_word";
    private const string TheFianna = "epic.the_fianna";

    // Windows Lug's Spear shuts down ("no Triskels until the clash ends" — the clash-end
    // window itself fires as the block lifts, so it stays open).
    private static bool IsClashScoped(ReactionTrigger t)
        => t is ReactionTrigger.ClashStarted or ReactionTrigger.AttackResolved
            or ReactionTrigger.CitadelStepEnded;

    /// <summary>
    /// Opens the window if anyone can react. Returns false (state untouched) otherwise —
    /// callers then continue inline exactly as before reactions existed.
    /// </summary>
    internal bool TryOpenReactionWindow(ReactionFrame frame)
    {
        if (IsClashScoped(frame.Trigger) && State.ActiveClash?.TriskelsBlocked == true) return false;

        frame.Queue.Clear();
        foreach (var pid in ReactionQueueOrder(frame))
        {
            var p = State.PlayerById(pid);
            if (p is not null && EligibleReactionCards(frame, p).Count > 0) frame.Queue.Add(pid);
        }
        if (frame.Queue.Count == 0) return false;

        frame.Cursor = 0;
        State.ReactionStack.Add(frame);
        PromptReaction(frame);
        return true;
    }

    /// <summary>Candidate reactors for a trigger, in turn order.</summary>
    private IEnumerable<string> ReactionQueueOrder(ReactionFrame frame)
    {
        switch (frame.Trigger)
        {
            case ReactionTrigger.ActionCardPlayed:
            case ReactionTrigger.NonActionCardPlayed:
            {
                // Opponents of the card's player, starting left of them.
                var start = State.Players.FindIndex(p => p.PlayerId == frame.TriggerPlayerId);
                return TurnOrderFrom(NextSeat(start)).Select(s => State.Players[s].PlayerId)
                    .Where(pid => pid != frame.TriggerPlayerId);
            }
            case ReactionTrigger.ClashStarted:
            case ReactionTrigger.CitadelStepEnded:
            case ReactionTrigger.ClashEnded:
                return State.ActiveClash?.Order ?? Enumerable.Empty<string>();
            case ReactionTrigger.GeisCancelled:
            case ReactionTrigger.EpicTalePlayed:
                return frame.TriggerPlayerId is { } pid ? new[] { pid } : Enumerable.Empty<string>();
            case ReactionTrigger.AttackResolved:
                // The attacker reacts first (Bard/Raid/Streng's Resolve), then the defender
                // (Dagda's Club / Diarmuid and Gráinne) — active player first, per the FAQ.
                return new[] { frame.TriggerPlayerId, frame.TargetPlayerId }
                    .Where(x => x is not null).Cast<string>();
            case ReactionTrigger.TurnEnded:
                // Any holder, starting left of the player whose turn just ended.
                return TurnOrderFrom(NextSeat(State.CurrentPlayerIndex))
                    .Select(s => State.Players[s].PlayerId);
            case ReactionTrigger.CardFollowUp:
            case ReactionTrigger.AssemblySetAside:
                return frame.TargetPlayerId is { } tid ? new[] { tid } : Enumerable.Empty<string>();
            default:
                return Enumerable.Empty<string>();
        }
    }

    /// <summary>The reaction cards <paramref name="reactor"/> may legally play in this window.</summary>
    internal List<string> EligibleReactionCards(ReactionFrame frame, PlayerState reactor)
    {
        var list = new List<string>();
        var clash = State.ActiveClash;
        if (IsClashScoped(frame.Trigger) && clash?.TriskelsBlocked == true) return list;

        switch (frame.Trigger)
        {
            case ReactionTrigger.ActionCardPlayed:
                if (reactor.PlayerId != frame.TriggerPlayerId && reactor.Hand.Contains(Geis))
                    list.Add(Geis);
                break;
            case ReactionTrigger.NonActionCardPlayed:
                if (reactor.PlayerId != frame.TriggerPlayerId && reactor.Hand.Contains(TheDagda))
                    list.Add(TheDagda);
                break;
            case ReactionTrigger.GeisCancelled:
                if (reactor.Hand.Contains(LugSamildanach)) list.Add(LugSamildanach);
                break;
            case ReactionTrigger.EpicTalePlayed:
                if (reactor.Hand.Contains(MasterCraftsman)) list.Add(MasterCraftsman);
                break;
            case ReactionTrigger.ClashStarted:
                if (reactor.Hand.Contains(Warlord) && reactor.ClanReserve > 0) list.Add(Warlord);
                break;
            case ReactionTrigger.CitadelStepEnded:
                if (reactor.Hand.Contains(BattleFrenzy) && clash?.ShelteredTotal > 0)
                    list.Add(BattleFrenzy);
                break;
            case ReactionTrigger.ClashEnded:
                if (reactor.Hand.Contains(DagdasCauldron) && clash is not null
                    && clash.RemovedClans.GetValueOrDefault(reactor.Color) > 0
                    && reactor.ClanReserve > 0)
                    list.Add(DagdasCauldron);
                break;
            case ReactionTrigger.AttackResolved:
                if (reactor.PlayerId == frame.TriggerPlayerId)
                {
                    if (frame.ClansRemoved && reactor.Hand.Contains(BardCard)) list.Add(BardCard);
                    if (reactor.Hand.Contains(RaidCard)) list.Add(RaidCard);
                    if (reactor.Hand.Contains(StrengsResolve)) list.Add(StrengsResolve);
                }
                else if (reactor.PlayerId == frame.TargetPlayerId && frame.ClansRemoved
                         && reactor.ClanReserve > 0)
                {
                    if (reactor.Hand.Contains(DagdasClub)) list.Add(DagdasClub);
                    if (reactor.Hand.Contains(DiarmuidGrainne) && State.Territories.Values.Any(t =>
                            t.InstanceId != frame.TerritoryId && t.IsPresent(reactor.Color)))
                        list.Add(DiarmuidGrainne);
                }
                break;
            case ReactionTrigger.TurnEnded:
                if (reactor.Hand.Contains(OengusPloy)) list.Add(OengusPloy);
                break;
            case ReactionTrigger.AssemblySetAside:
                if (reactor.PlayerId == frame.TargetPlayerId && reactor.Hand.Contains(CathbadsWord))
                    list.Add(CathbadsWord);
                break;
            case ReactionTrigger.CardFollowUp:
                // The follow-up "reaction" is the trigger card's own continuation — the targeted
                // player answers it (e.g. Coalition's partner deciding to move along) or passes.
                if (reactor.PlayerId == frame.TargetPlayerId && frame.TriggerCardId is { } tc)
                    list.Add(tc);
                break;
        }

        // Lug's Spear may be thrown into any clash-scoped window to shut Triskels down.
        if (IsClashScoped(frame.Trigger) && clash is not null && reactor.Hand.Contains(LugsSpear))
            list.Add(LugsSpear);

        return list;
    }

    private void PromptReaction(ReactionFrame frame)
        => State.Pending = new PendingDecision
        {
            Kind = PendingKind.Reaction,
            PlayerId = frame.Queue[frame.Cursor],
            CardId = frame.TriggerCardId,
            Trigger = frame.Trigger.ToString(),
        };

    private void ApplyReaction(Move move)
    {
        var frame = State.ReactionStack[^1];
        var reactor = State.PlayerById(State.Pending!.PlayerId)!;

        if (move.Type == MoveType.PassReaction)
        {
            frame.Cursor++;
            AdvanceOrCloseWindow(frame);
            return;
        }

        Require(move.Type == MoveType.PlayReaction, "Expected a reaction: play a card or pass.");
        var cid = move.CardId ?? throw new InvalidOperationException("PlayReaction needs a card id.");
        Require(EligibleReactionCards(frame, reactor).Contains(cid), "That card cannot react to this.");
        ExecuteReaction(frame, reactor, cid, move);
    }

    /// <summary>Prompts the next queued reactor who can still react, or closes the window.</summary>
    private void AdvanceOrCloseWindow(ReactionFrame frame)
    {
        while (frame.Cursor < frame.Queue.Count)
        {
            var p = State.PlayerById(frame.Queue[frame.Cursor]);
            if (p is not null && EligibleReactionCards(frame, p).Count > 0)
            {
                PromptReaction(frame);
                return;
            }
            frame.Cursor++;
        }
        CloseWindow(frame);
    }

    private void CloseWindow(ReactionFrame frame)
    {
        State.ReactionStack.Remove(frame);
        RunContinuation(frame);
    }

    private void RunContinuation(ReactionFrame frame)
    {
        switch (frame.Continuation)
        {
            case ReactionContinuation.ResolvePlayedCard:
            {
                var player = State.PlayerById(frame.TriggerPlayerId!)!;
                var def = Data.Card(frame.TriggerCardId!);
                var move = frame.TriggerMove
                    ?? new Move { Type = MoveType.PlayCard, PlayerId = player.PlayerId, CardId = def.Id };
                FinishPlayCard(player, def, move, frame.Cancelled);
                break;
            }
            case ReactionContinuation.DiscardPlayedCard:
                DiscardPlayed(Data.Card(frame.TriggerCardId!));
                AfterCardPlayed();
                break;
            case ReactionContinuation.BeginCitadelStep:
                BeginCitadelStep();
                break;
            case ReactionContinuation.AfterManeuver:
                AfterManeuver();
                break;
            case ReactionContinuation.ResumeSeasonTurn:
                if (State.ActiveClash is null) AdvanceSeasonTurn();
                break;
            case ReactionContinuation.CoalitionClash:
            {
                // Both movers are done; a contested destination now clashes, with the two
                // marked as coalition partners (no Citadels, no attacking each other).
                var dest = State.Territories[frame.SecondaryTerritoryId!];
                if (dest.Clans.Count(kv => kv.Value > 0) > 1)
                {
                    var partners = new List<string> { frame.TriggerPlayerId! };
                    if (frame.TargetPlayerId is { } partner) partners.Add(partner);
                    StartClash(dest.InstanceId, frame.TriggerPlayerId!, partners);
                }
                AfterCardPlayed();
                break;
            }
            case ReactionContinuation.ResumeClashResolution:
                PromptNextManeuver();
                break;
            case ReactionContinuation.FinishEndClash:
                FinishEndClash();
                break;
            case ReactionContinuation.AdvanceTurn:
                AdvanceSeasonTurnCore();
                break;
            case ReactionContinuation.FinishAssemblyDeal:
                FinishAssemblyDeal();
                break;
        }
    }

    private void ExecuteReaction(ReactionFrame frame, PlayerState reactor, string cid, Move move)
    {
        switch (cid)
        {
            case Geis:
            {
                reactor.Hand.Remove(Geis);
                State.ActionDiscard.Add(Geis);
                Emit("ReactionPlayed", reactor.PlayerId, Geis);
                Emit("CardCancelled", frame.TriggerPlayerId, frame.TriggerCardId);
                State.ReactionStack.Remove(frame);

                // The victim may answer with Lug Samildanach to keep the cancelled card.
                var nested = new ReactionFrame
                {
                    Trigger = ReactionTrigger.GeisCancelled,
                    TriggerPlayerId = frame.TriggerPlayerId,
                    TriggerCardId = frame.TriggerCardId,
                    TriggerMove = frame.TriggerMove,
                    Continuation = frame.Continuation,
                    Cancelled = true,
                };
                if (!TryOpenReactionWindow(nested)) RunContinuation(nested);
                break;
            }

            case LugSamildanach:
            {
                reactor.Hand.Remove(LugSamildanach);
                State.EpicDiscard.Add(LugSamildanach);
                reactor.Hand.Add(frame.TriggerCardId!); // the cancelled card is kept, unresolved
                Emit("ReactionPlayed", reactor.PlayerId, LugSamildanach, Detail: frame.TriggerCardId);
                State.ReactionStack.Remove(frame);
                AfterCardPlayed();
                break;
            }

            case MasterCraftsman:
            {
                reactor.Hand.Remove(MasterCraftsman);
                State.ActionDiscard.Add(MasterCraftsman);
                var recipient = State.PlayerById(move.TargetPlayerId ?? "")
                    ?? State.Players[NextSeat(State.Players.IndexOf(reactor))];
                if (recipient == reactor) recipient = State.Players[NextSeat(State.Players.IndexOf(reactor))];
                recipient.Hand.Add(frame.TriggerCardId!); // the epic passes on instead of discarding
                GainDeed(reactor);
                Emit("ReactionPlayed", reactor.PlayerId, MasterCraftsman, Detail: recipient.PlayerId);
                State.ReactionStack.Remove(frame);
                AfterCardPlayed();
                break;
            }

            case Warlord:
            {
                var clash = State.ActiveClash!;
                reactor.Hand.Remove(Warlord);
                State.ActionDiscard.Add(Warlord);
                PlaceClans(reactor, State.Territories[clash.TerritoryId], 1);
                if (move.TargetPlayerId is { } forced && clash.Order.Contains(forced))
                    clash.ForcedFirstManeuverId = forced;
                Emit("ReactionPlayed", reactor.PlayerId, Warlord, TerritoryId: clash.TerritoryId);
                AdvanceOrCloseWindow(frame); // other Warlord holders may still join
                break;
            }

            case BardCard:
            {
                reactor.Hand.Remove(BardCard);
                State.ActionDiscard.Add(BardCard);
                GainDeed(reactor);
                Emit("ReactionPlayed", reactor.PlayerId, BardCard);
                AdvanceOrCloseWindow(frame); // the same attacker may still play Raid
                break;
            }

            case RaidCard:
            {
                reactor.Hand.Remove(RaidCard);
                State.ActionDiscard.Add(RaidCard);
                var target = State.PlayerById(frame.TargetPlayerId!)!;
                var actions = target.Hand
                    .Where(c => Data.TryGetCard(c, out var d) && d.Type == CardType.Action).ToList();
                if (actions.Count > 0)
                {
                    var stolen = actions[_rng.Next(actions.Count)];
                    target.Hand.Remove(stolen);
                    reactor.Hand.Add(stolen);
                    Emit("ReactionPlayed", reactor.PlayerId, RaidCard, Detail: target.PlayerId);
                }
                else
                {
                    // No Action card to take: remove one of their exposed clans instead.
                    if (State.ActiveClash is { } cl && Exposed(cl, target.Color) > 0)
                        RemoveClan(State.Territories[cl.TerritoryId], target.Color);
                    Emit("ReactionPlayed", reactor.PlayerId, RaidCard, Detail: target.PlayerId);
                }
                AdvanceOrCloseWindow(frame);
                break;
            }

            case LugsSpear:
            {
                reactor.Hand.Remove(LugsSpear);
                State.EpicDiscard.Add(LugsSpear);
                State.ActiveClash!.TriskelsBlocked = true;
                Emit("ReactionPlayed", reactor.PlayerId, LugsSpear);
                State.ReactionStack.Remove(frame);
                RunContinuation(frame); // the window slams shut for everyone
                break;
            }

            case TheDagda:
            {
                reactor.Hand.Remove(TheDagda);
                State.EpicDiscard.Add(TheDagda);
                Emit("ReactionPlayed", reactor.PlayerId, TheDagda);
                Emit("CardCancelled", frame.TriggerPlayerId, frame.TriggerCardId);
                frame.Cancelled = true;
                State.ReactionStack.Remove(frame);
                RunContinuation(frame); // the cancelled Epic/Advantage is still discarded
                break;
            }

            case BattleFrenzy:
            {
                var clash = State.ActiveClash!;
                reactor.Hand.Remove(BattleFrenzy);
                State.EpicDiscard.Add(BattleFrenzy);
                clash.Sheltered.Clear(); // everyone tumbles out of the citadels
                Emit("ReactionPlayed", reactor.PlayerId, BattleFrenzy, TerritoryId: clash.TerritoryId);
                State.ReactionStack.Remove(frame);
                RunContinuation(frame);
                break;
            }

            case DagdasCauldron:
            {
                var clash = State.ActiveClash!;
                var territory = State.Territories[clash.TerritoryId];
                reactor.Hand.Remove(DagdasCauldron);
                State.EpicDiscard.Add(DagdasCauldron);
                var lost = clash.RemovedClans.GetValueOrDefault(reactor.Color);
                var back = Math.Min(lost, reactor.ClanReserve);
                if (back > 0)
                {
                    territory.AddClans(reactor.Color, back);
                    reactor.ClanReserve -= back;
                    clash.RemovedClans[reactor.Color] = 0;
                }
                Emit("ReactionPlayed", reactor.PlayerId, DagdasCauldron, TerritoryId: territory.InstanceId,
                    Detail: back.ToString());
                AdvanceOrCloseWindow(frame);
                break;
            }

            case StrengsResolve:
            {
                reactor.Hand.Remove(StrengsResolve);
                State.EpicDiscard.Add(StrengsResolve);
                GainDeed(reactor);
                Emit("ReactionPlayed", reactor.PlayerId, StrengsResolve);
                AdvanceOrCloseWindow(frame); // Bard/Raid may still follow
                break;
            }

            case DagdasClub:
            {
                // The clan the attack just removed is not lost after all.
                var territory = State.Territories[frame.TerritoryId!];
                reactor.Hand.Remove(DagdasClub);
                State.EpicDiscard.Add(DagdasClub);
                territory.AddClans(reactor.Color, 1);
                reactor.ClanReserve--;
                if (State.ActiveClash is { } cl && cl.RemovedClans.GetValueOrDefault(reactor.Color) > 0)
                    cl.RemovedClans[reactor.Color]--;
                Emit("ReactionPlayed", reactor.PlayerId, DagdasClub, TerritoryId: territory.InstanceId);
                AdvanceOrCloseWindow(frame);
                break;
            }

            case DiarmuidGrainne:
            {
                // The removed clan flees to a different territory where its player is present.
                var dest = Territory(move.TerritoryId);
                if (dest is null || dest.InstanceId == frame.TerritoryId || !dest.IsPresent(reactor.Color))
                    dest = State.Territories.Values.First(t =>
                        t.InstanceId != frame.TerritoryId && t.IsPresent(reactor.Color));
                reactor.Hand.Remove(DiarmuidGrainne);
                State.EpicDiscard.Add(DiarmuidGrainne);
                dest.AddClans(reactor.Color, 1);
                reactor.ClanReserve--;
                if (State.ActiveClash is { } acl && acl.RemovedClans.GetValueOrDefault(reactor.Color) > 0)
                    acl.RemovedClans[reactor.Color]--;
                Emit("ReactionPlayed", reactor.PlayerId, DiarmuidGrainne, TerritoryId: dest.InstanceId);
                AdvanceOrCloseWindow(frame);
                break;
            }

            case OengusPloy:
            {
                reactor.Hand.Remove(OengusPloy);
                State.EpicDiscard.Add(OengusPloy);
                Emit("ReactionPlayed", reactor.PlayerId, OengusPloy);
                State.ReactionStack.Remove(frame);
                // The holder seizes the next turn; pass counting is otherwise unchanged.
                State.CurrentPlayerIndex = State.Players.IndexOf(reactor);
                State.Pending = new PendingDecision { Kind = PendingKind.SeasonTurn, PlayerId = reactor.PlayerId };
                break;
            }

            case CathbadsWord:
            {
                var deck = State.StagedActionDeck!;
                reactor.Hand.Remove(CathbadsWord);
                State.EpicDiscard.Add(CathbadsWord);
                var chosen = move.CardIds?.FirstOrDefault(deck.Contains) ?? deck[0];
                deck.Remove(chosen);
                State.SetAsideActionCard = chosen;
                Emit("ReactionPlayed", reactor.PlayerId, CathbadsWord);
                State.ReactionStack.Remove(frame);
                RunContinuation(frame);
                break;
            }

            case CoalitionCard when frame.Trigger == ReactionTrigger.CardFollowUp:
            {
                // The named partner moves clans from the shared territory to the same destination.
                var from = State.Territories[frame.TerritoryId!];
                var to = State.Territories[frame.SecondaryTerritoryId!];
                var n = Math.Min(move.Amount > 0 ? move.Amount : 1, from.ClansOf(reactor.Color));
                if (n > 0)
                {
                    from.AddClans(reactor.Color, -n);
                    to.AddClans(reactor.Color, n); // the clash check happens in the continuation
                    Emit("ClansMoved", reactor.PlayerId, TerritoryId: to.InstanceId, Detail: n.ToString());
                }
                Emit("ReactionPlayed", reactor.PlayerId, CoalitionCard);
                State.ReactionStack.Remove(frame);
                RunContinuation(frame);
                break;
            }

            default:
                throw new InvalidOperationException($"Card {cid} has no reactive behaviour.");
        }
    }
}
