using Inis.Core.Data;
using Inis.Core.Effects;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Util;

namespace Inis.Core.Rules;

/// <summary>
/// The authoritative INIS rules engine. Deterministic from its seed: it owns a
/// <see cref="GameState"/>, exposes the legal moves for whoever must act
/// (<see cref="Pending"/>), and mutates state only through <see cref="Apply"/>. Both the
/// server and the client's embedded host drive a game exclusively through this type, so
/// there is a single rules implementation. See docs/rules.md for the modeled rules and the
/// (documented) simplifications around Triskel reactive timing.
/// </summary>
public sealed partial class GameEngine
{
    public GameState State { get; }
    public GameData Data { get; }
    private readonly DeterministicRng _rng;
    private readonly List<GameEvent> _events = new();

    public GameEngine(GameState state, GameData? data = null)
    {
        State = state;
        Data = data ?? GameData.Default;
        // Resume the RNG at the persisted cursor so a reconstructed game (e.g. reloaded from
        // the database) keeps drawing the identical deterministic sequence.
        _rng = new DeterministicRng(state.Seed, state.RngCursor);
    }

    public PendingDecision? Pending => State.Pending;
    public IReadOnlyList<GameEvent> LastEvents => _events;

    // ------------------------------------------------------------------ setup

    /// <summary>Creates a fully set-up game and advances it to the first draft pick.</summary>
    public static GameEngine Create(string gameId, int seed, IReadOnlyList<SeatConfig> seats,
        GameData? data = null, GameOptions? options = null)
    {
        options ??= GameOptions.Base;
        if (seats.Count < 2 || seats.Count > options.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(seats),
                $"{options.Label} supports 2–{options.MaxPlayers} players.");

        data ??= GameData.Default;
        var state = new GameState { GameId = gameId, Seed = seed, Options = options, PretendersRemaining = seats.Count };
        foreach (var s in seats)
            state.Players.Add(new PlayerState
            {
                PlayerId = s.PlayerId, DisplayName = s.DisplayName, Color = s.Color, IsAi = s.IsAi,
                ClanReserve = 12,
            });

        var engine = new GameEngine(state, data);
        engine.BuildEpicDeck();
        engine.SetupBoard(seats.Count);
        engine.BeginAssembly();
        engine.SyncRngCursor();
        return engine;
    }

    /// <summary>Writes the live RNG position back into the (persistable) state.</summary>
    private void SyncRngCursor() => State.RngCursor = _rng.Cursor;

    private void BuildEpicDeck()
    {
        foreach (var c in Data.Cards.Where(c => c.Type == CardType.EpicTale))
            for (var i = 0; i < c.Count; i++) State.EpicDeck.Add(c.Id);
        _rng.Shuffle(State.EpicDeck);
    }

    private void SetupBoard(int playerCount)
    {
        // Initial tiles: one per player, placed in a ring so each touches two others.
        var tilePool = Data.Territories.Select(t => t.Id).ToList();
        _rng.Shuffle(tilePool);
        var placed = new List<TerritoryState>();
        for (var i = 0; i < playerCount; i++)
        {
            var def = tilePool[i];
            var inst = new TerritoryState { InstanceId = $"T{i}", DefinitionId = def };
            State.Territories[inst.InstanceId] = inst;
            placed.Add(inst);
        }
        for (var i = 0; i < placed.Count; i++)
        {
            var a = placed[i];
            var b = placed[(i + 1) % placed.Count];
            if (a == b) continue;
            a.Adjacent.Add(b.InstanceId);
            b.Adjacent.Add(a.InstanceId);
        }

        // Brenn chooses the Capital (deterministically the first placed tile) + a Sanctuary.
        var capital = placed[0];
        capital.HasCapital = true;
        State.CapitalInstanceId = capital.InstanceId;
        State.CitadelsRemaining--;            // the Capital is one of the 10 Citadels
        capital.Sanctuaries++;
        State.SanctuariesRemaining--;

        State.BrennIndex = _rng.Next(playerCount);
        State.Direction = _rng.Next(2) == 0 ? TurnDirection.Clockwise : TurnDirection.CounterClockwise;

        // Starting clans: each player places 2, spread deterministically over the tiles.
        for (var k = 0; k < 2; k++)
            for (var p = 0; p < playerCount; p++)
            {
                var seat = (State.BrennIndex + p) % playerCount;
                var player = State.Players[seat];
                var tile = placed[(seat + k) % placed.Count];
                tile.AddClans(player.Color, 1);
                player.ClanReserve--;
            }
    }

    // --------------------------------------------------------------- assembly

    private void BeginAssembly()
    {
        State.Phase = GamePhase.Assembly;
        State.ConsecutivePasses = 0;
        State.BrennHasOpened = false;

        AssignBrenn();
        if (CheckForVictory()) return;     // sets GameOver if someone won
        TakeAdvantageCards();
        FlipFlockOfCrows();
        DealAndBeginDraft();
    }

    private void AssignBrenn()
    {
        if (State.CapitalInstanceId is null) return;
        var capital = State.Territories[State.CapitalInstanceId];
        var chief = capital.Chieftain();
        if (chief is { } color)
        {
            var idx = State.Players.FindIndex(p => p.Color == color);
            if (idx >= 0) State.BrennIndex = idx;
        }
    }

    /// <summary>Assembly step 2. Returns true if the game ended with a winner.</summary>
    private bool CheckForVictory()
    {
        var pretenders = State.Players.Where(p => p.HasPretenderToken).ToList();
        if (pretenders.Count > 0)
        {
            var best = pretenders
                .Select(p => (p, n: VictoryEvaluator.CountConditionsMet(State, p)))
                .Where(x => x.n >= 1)
                .ToList();
            if (best.Count > 0)
            {
                var max = best.Max(x => x.n);
                var tied = best.Where(x => x.n == max).Select(x => x.p).ToList();
                PlayerState? winner =
                    tied.Count == 1 ? tied[0] :
                    tied.Contains(State.Brenn) ? State.Brenn : null;
                if (winner is not null)
                {
                    State.WinnerId = winner.PlayerId;
                    State.Phase = GamePhase.GameOver;
                    State.Pending = new PendingDecision { Kind = PendingKind.GameOver, PlayerId = winner.PlayerId };
                    Emit("GameOver", winner.PlayerId);
                    return true;
                }
            }
        }
        foreach (var p in State.Players) p.HasPretenderToken = false;
        State.PretendersRemaining = State.Players.Count;
        return false;
    }

    private void TakeAdvantageCards()
    {
        foreach (var t in State.Territories.Values)
        {
            var chief = t.Chieftain();
            if (chief is not { } color) continue;
            var def = Data.Territory(t.DefinitionId);
            if (def.AdvantageCardId is not { } adv) continue;
            var player = State.PlayerByColor(color);
            if (player is not null && !player.Advantages.Contains(adv)) player.Advantages.Add(adv);
        }
    }

    private void FlipFlockOfCrows()
        => State.Direction = _rng.Next(2) == 0 ? TurnDirection.Clockwise : TurnDirection.CounterClockwise;

    private void DealAndBeginDraft()
    {
        State.AssemblyStep = AssemblyStep.Draft;
        var n = State.Players.Count;

        // Build the Action deck for this game's content set (base vs. Seasons of Inis) and player
        // count. In a Seasons game the expansion cards join the deck and their "updated" variants
        // replace the matching base cards; 4-player-only cards drop below four players.
        var seasons = State.Options.SeasonsOfInis;
        var replacedBaseIds = Data.Cards
            .Where(c => c.Expansion is not null && c.ResolvedEffectId != c.Id)
            .Select(c => c.ResolvedEffectId).ToHashSet();

        var copies = State.Options.DeckCopies;
        var deck = new List<string>();
        foreach (var c in Data.Cards.Where(c => c.Type == CardType.Action))
        {
            if (!IncludeAction(c, n, seasons, replacedBaseIds)) continue;
            for (var i = 0; i < c.Count * copies; i++) deck.Add(c.Id);
        }
        _rng.Shuffle(deck);
        State.SetAsideActionCard = deck[0];
        deck.RemoveAt(0);

        var perHand = n == 2 ? 3 : 4;
        var keepCounts = n == 2 ? new[] { 1, 2 } : new[] { 1, 2, 3 };
        var hands = new List<string>[n];
        var held = new List<string>[n];
        var accumulated = new List<string>[n];
        for (var i = 0; i < n; i++)
        {
            hands[i] = new List<string>();
            held[i] = new List<string>();
            accumulated[i] = new List<string>();
        }
        DealHands(deck, hands, perHand);

        State.Draft = new DraftState
        {
            KeepCounts = keepCounts, Hands = hands, Held = held, Accumulated = accumulated,
            SubDraftCount = n == 2 ? 2 : 1, Round = 0, SubDraft = 0, PickerSeat = State.BrennIndex,
            // Stash the leftover deck for the 2-player second sub-draft (persisted in state).
            LeftoverDeck = deck,
        };
        State.Pending = new PendingDecision { Kind = PendingKind.Draft, PlayerId = State.Players[State.BrennIndex].PlayerId };
    }

    /// <summary>Decides whether an Action card belongs in the deck for this game's content set.</summary>
    private static bool IncludeAction(CardDefinition card, int playerCount, bool seasons,
        HashSet<string> replacedBaseIds)
    {
        var isExpansion = card.Expansion is not null;
        if (!seasons)
            // Base game: expansion cards never appear; 4-player-only cards only with four+ players.
            return !isExpansion && (playerCount >= 4 || !card.FourPlayerOnly);

        // Seasons of Inis: add the expansion cards; drop the base cards their variants replace.
        if (isExpansion) return true;
        return !replacedBaseIds.Contains(card.Id) && (playerCount >= 4 || !card.FourPlayerOnly);
    }

    private static void DealHands(List<string> deck, List<string>[] hands, int perHand)
    {
        for (var c = 0; c < perHand; c++)
            for (var i = 0; i < hands.Length; i++)
                hands[i].Add(deck.TakeFirst());
    }

    // ------------------------------------------------------------ draft logic

    private void ApplyDraftPick(Move move)
    {
        var d = State.Draft!;
        var seat = d.PickerSeat;
        var hand = d.Hands[seat];
        var card = move.CardId ?? hand.FirstOrDefault()
            ?? throw new InvalidOperationException("No card to draft.");
        if (!hand.Remove(card)) throw new InvalidOperationException("Card not available to draft.");
        d.Held[seat].Add(card);

        var keep = d.KeepCounts[d.Round];
        if (d.Held[seat].Count < keep)
        {
            // Same seat keeps picking until they have held the round's quota.
            State.Pending = DraftPending(seat);
            return;
        }
        // Seat done for this round; move to the next seat that still needs to pick.
        AdvanceDraftSeat();
    }

    private void AdvanceDraftSeat()
    {
        var d = State.Draft!;
        var keep = d.KeepCounts[d.Round];
        // Find the next seat (in turn order) that has not yet held this round's quota.
        foreach (var seat in TurnOrderFrom(State.BrennIndex))
            if (d.Held[seat].Count < keep)
            {
                d.PickerSeat = seat;
                State.Pending = DraftPending(seat);
                return;
            }
        // Everyone finished the round: pass the leftover hands and combine.
        RotateAndCombine();
    }

    private void RotateAndCombine()
    {
        var d = State.Draft!;
        var n = State.Players.Count;
        var passed = new List<string>[n];
        for (var i = 0; i < n; i++) passed[i] = d.Hands[i]; // remaining = pass pile

        var newHands = new List<string>[n];
        for (var i = 0; i < n; i++) newHands[i] = new List<string>();
        // Pass each seat's leftover to the next seat in turn direction.
        for (var i = 0; i < n; i++)
        {
            var dest = NextSeat(i);
            newHands[dest].AddRange(passed[i]);
        }
        // Combine received cards with the cards held this round.
        for (var i = 0; i < n; i++)
        {
            newHands[i].AddRange(d.Held[i]);
            d.Hands[i] = newHands[i];
            d.Held[i].Clear();
        }

        d.Round++;
        if (d.Round >= d.KeepCounts.Length)
        {
            FinishSubDraft();
            return;
        }
        d.PickerSeat = State.BrennIndex;
        State.Pending = DraftPending(State.BrennIndex);
    }

    private void FinishSubDraft()
    {
        var d = State.Draft!;
        var n = State.Players.Count;
        // The final hands (held + last received) are the drafted cards for this sub-draft.
        for (var i = 0; i < n; i++) d.Accumulated[i].AddRange(d.Hands[i]);

        d.SubDraft++;
        if (d.SubDraft < d.SubDraftCount)
        {
            // 2-player: deal a fresh set of 3 and run the second sub-draft.
            for (var i = 0; i < n; i++) { d.Hands[i].Clear(); d.Held[i].Clear(); }
            DealHands(d.LeftoverDeck, d.Hands, 3);
            d.Round = 0;
            d.PickerSeat = State.BrennIndex;
            State.Pending = DraftPending(State.BrennIndex);
            return;
        }

        // Draft complete: drafted action cards go into hands; begin the Season.
        for (var i = 0; i < n; i++) State.Players[i].Hand.AddRange(d.Accumulated[i]);
        State.Draft = null;
        BeginSeason();
    }

    private PendingDecision DraftPending(int seat)
        => new() { Kind = PendingKind.Draft, PlayerId = State.Players[seat].PlayerId };

    // ----------------------------------------------------------- season phase

    private void BeginSeason()
    {
        State.Phase = GamePhase.Season;
        State.RoundNumber = State.RoundNumber; // unchanged here; bumped at end of season
        State.ConsecutivePasses = 0;
        State.BrennHasOpened = false;
        State.CurrentPlayerIndex = State.BrennIndex;
        foreach (var p in State.Players) p.HasPassed = false;
        State.Pending = new PendingDecision { Kind = PendingKind.SeasonTurn, PlayerId = State.Brenn.PlayerId };
    }

    private void AdvanceSeasonTurn()
    {
        if (State.ConsecutivePasses >= State.Players.Count)
        {
            EndSeason();
            return;
        }
        State.CurrentPlayerIndex = NextSeat(State.CurrentPlayerIndex);
        State.Pending = new PendingDecision { Kind = PendingKind.SeasonTurn, PlayerId = State.CurrentPlayer.PlayerId };
    }

    private void EndSeason()
    {
        // Discard Action cards; keep Epic Tales; return unplayed advantages to the supply
        // (the next Assembly re-deals them to the current chieftains).
        foreach (var p in State.Players)
        {
            var keep = new List<string>();
            foreach (var cid in p.Hand)
            {
                if (!Data.TryGetCard(cid, out var def)) { keep.Add(cid); continue; }
                if (def.Type == CardType.Action) State.ActionDiscard.Add(cid);
                else if (def.Type == CardType.Advantage) { /* legacy saves only; returned to supply */ }
                else keep.Add(cid); // Epic Tales stay
            }
            p.Hand.Clear();
            p.Hand.AddRange(keep);
            p.Advantages.Clear();
        }
        foreach (var t in State.Territories.Values) t.HasFestival = false;
        State.RoundNumber++;
        BeginAssembly();
    }

    // ------------------------------------------------------------------- API

    /// <summary>Applies a move from the pending player (or <see cref="Move.PlayerId"/>).</summary>
    public IReadOnlyList<GameEvent> Apply(Move move)
    {
        _events.Clear();
        if (State.Phase == GamePhase.GameOver) throw new InvalidOperationException("The game is over.");
        var pending = State.Pending ?? throw new InvalidOperationException("No pending decision.");
        var actor = move.PlayerId ?? pending.PlayerId;

        State.IntentLog.Add($"{actor}:{move.Type}:{move.CardId}:{move.TerritoryId}");

        switch (pending.Kind)
        {
            case PendingKind.Draft:
                Require(move.Type == MoveType.DraftPick, "Expected a draft pick.");
                RequireActor(actor, pending);
                ApplyDraftPick(move);
                break;
            case PendingKind.SeasonTurn:
                RequireActor(actor, pending);
                ApplySeasonMove(move);
                break;
            case PendingKind.ClashShelter:
                RequireActor(actor, pending);
                ApplyShelter(move);
                break;
            case PendingKind.ClashManeuver:
                RequireActor(actor, pending);
                ApplyManeuver(move);
                break;
            case PendingKind.AttackResponse:
                RequireActor(actor, pending);
                ApplyAttackResponse(move);
                break;
            case PendingKind.Reaction:
                RequireActor(actor, pending);
                ApplyReaction(move);
                break;
            default:
                throw new InvalidOperationException($"Cannot act during {pending.Kind}.");
        }
        SyncRngCursor();
        return _events;
    }

    private void ApplySeasonMove(Move move)
    {
        var player = State.CurrentPlayer;
        switch (move.Type)
        {
            case MoveType.Pass:
                Require(!(player == State.Brenn && !State.BrennHasOpened), "The Brenn must open the Season.");
                player.HasPassed = true;
                State.ConsecutivePasses++;
                Emit("Pass", player.PlayerId);
                AdvanceSeasonTurn();
                break;

            case MoveType.TakePretender:
                Require(!(player == State.Brenn && !State.BrennHasOpened), "The Brenn must open the Season.");
                Require(!player.HasPretenderToken, "You already hold a Pretender token.");
                Require(State.PretendersRemaining > 0, "No Pretender tokens left.");
                Require(VictoryEvaluator.MeetsAny(State, player), "You meet no victory condition.");
                player.HasPretenderToken = true;
                State.PretendersRemaining--;
                State.ConsecutivePasses = 0;
                Emit("TakePretender", player.PlayerId);
                AdvanceSeasonTurn();
                break;

            case MoveType.PlayCard:
                PlayCard(player, move);
                break;

            case MoveType.Resign:
                player.HasPassed = true;
                Emit("Resign", player.PlayerId);
                AdvanceSeasonTurn();
                break;

            default:
                throw new InvalidOperationException($"Illegal Season move: {move.Type}.");
        }
    }

    private void PlayCard(PlayerState player, Move move)
    {
        var cid = move.CardId ?? throw new InvalidOperationException("PlayCard needs a card id.");
        Require(player.Hand.Contains(cid) || player.Advantages.Contains(cid), "Card not in hand.");
        var def = Data.Card(cid);

        State.ConsecutivePasses = 0;
        State.BrennHasOpened = true;
        if (!player.Hand.Remove(cid)) player.Advantages.Remove(cid);
        Emit("CardPlayed", player.PlayerId, cid);

        // Opponents may interrupt an Action card with Geis before its effect resolves.
        if (def.Type == CardType.Action && TryOpenReactionWindow(new ReactionFrame
            {
                Trigger = ReactionTrigger.ActionCardPlayed,
                TriggerPlayerId = player.PlayerId,
                TriggerCardId = cid,
                TriggerMove = move,
                Continuation = ReactionContinuation.ResolvePlayedCard,
            }))
            return;

        FinishPlayCard(player, def, move, cancelled: false);
    }

    /// <summary>Resolves a played card once any pre-resolution reaction window has closed.</summary>
    private void FinishPlayCard(PlayerState player, CardDefinition def, Move move, bool cancelled)
    {
        if (!cancelled)
        {
            // Resolve the effect (one handler per card; unmodeled cards are a legal no-op).
            EffectRegistry.Resolve(this, player, def, move);

            // Master Craftsman may pass a just-resolved Epic Tale on instead of discarding it.
            if (def.Type == CardType.EpicTale && TryOpenReactionWindow(new ReactionFrame
                {
                    Trigger = ReactionTrigger.EpicTalePlayed,
                    TriggerPlayerId = player.PlayerId,
                    TriggerCardId = def.Id,
                    Continuation = ReactionContinuation.DiscardPlayedCard,
                }))
                return;
        }

        DiscardPlayed(def);
        AfterCardPlayed();
    }

    /// <summary>Discard by type (unless an effect kept it, e.g. a retained Epic Tale).</summary>
    private void DiscardPlayed(CardDefinition def)
    {
        if (def.Type == CardType.Action) State.ActionDiscard.Add(def.Id);
        else if (def.Type == CardType.EpicTale) State.EpicDiscard.Add(def.Id);
        // Advantage cards are set aside face-up; not tracked individually here.
    }

    /// <summary>Continues the turn after a card play fully finishes (effect + reactions + discard).</summary>
    private void AfterCardPlayed()
    {
        if (State.ActiveClash is not null)
        {
            // A reaction window may have overwritten a clash prompt set mid-effect; restore it.
            if (State.Pending?.Kind == PendingKind.Reaction && State.ReactionStack.Count == 0)
            {
                if (State.ActiveClash.InResolution) PromptNextManeuver();
                else PromptNextShelter();
            }
            return; // clash drives its own prompts
        }
        AdvanceSeasonTurn();
    }

    // ------------------------------------------------------- effect mutators
    // These are the verbs that card handlers call. They are deliberately small and
    // side-effect-explicit so behaviour is easy to test and reason about.

    public void DrawEpic(PlayerState player)
    {
        if (State.EpicDeck.Count == 0)
        {
            if (State.EpicDiscard.Count == 0) return;
            State.EpicDeck.AddRange(State.EpicDiscard);
            State.EpicDiscard.Clear();
            _rng.Shuffle(State.EpicDeck);
        }
        var card = State.EpicDeck.TakeFirst();
        player.Hand.Add(card);
        Emit("DrewEpic", player.PlayerId, card);
    }

    public void GainDeed(PlayerState player)
    {
        if (State.DeedsRemaining <= 0) return;
        player.Deeds++;
        State.DeedsRemaining--;
        Emit("DeedGained", player.PlayerId);
    }

    public void PlaceClans(PlayerState player, TerritoryState territory, int amount)
    {
        amount = Math.Min(amount, player.ClanReserve);
        if (amount <= 0) return;
        territory.AddClans(player.Color, amount);
        player.ClanReserve -= amount;
        Emit("ClansPlaced", player.PlayerId, TerritoryId: territory.InstanceId, Detail: amount.ToString());
    }

    public void RemoveClan(TerritoryState territory, ClanColor color)
    {
        if (territory.ClansOf(color) <= 0) return;
        territory.AddClans(color, -1);
        var owner = State.PlayerByColor(color);
        if (owner is not null) owner.ClanReserve++;
        Emit("ClanRemoved", owner?.PlayerId, TerritoryId: territory.InstanceId);
    }

    public void BuildCitadel(TerritoryState territory)
    {
        if (State.CitadelsRemaining <= 0) return;
        territory.Citadels++;
        State.CitadelsRemaining--;
        Emit("BuildingPlaced", TerritoryId: territory.InstanceId, Detail: "Citadel");
    }

    public void BuildSanctuary(TerritoryState territory)
    {
        if (State.SanctuariesRemaining <= 0) return;
        territory.Sanctuaries++;
        State.SanctuariesRemaining--;
        Emit("BuildingPlaced", TerritoryId: territory.InstanceId, Detail: "Sanctuary");
    }

    /// <summary>Moves clans between territories; starts a clash if opponents are present at the destination.</summary>
    public void MoveClans(PlayerState player, TerritoryState from, TerritoryState to, int amount)
    {
        amount = Math.Min(amount, from.ClansOf(player.Color));
        if (amount <= 0) return;
        from.AddClans(player.Color, -amount);
        to.AddClans(player.Color, amount);
        Emit("ClansMoved", player.PlayerId, TerritoryId: to.InstanceId, Detail: amount.ToString());
        if (to.Clans.Any(kv => kv.Key != player.Color && kv.Value > 0))
            StartClash(to.InstanceId, player.PlayerId);
    }

    public void TakeAdvantage(PlayerState player, string advantageId)
    {
        // Face-up zone: advantages are public information (never redacted).
        foreach (var other in State.Players) other.Advantages.Remove(advantageId);
        if (!player.Advantages.Contains(advantageId)) player.Advantages.Add(advantageId);
        Emit("AdvantageTaken", player.PlayerId, advantageId);
    }

    public TerritoryState? Territory(string? instanceId)
        => instanceId is not null && State.Territories.TryGetValue(instanceId, out var t) ? t : null;

    // ------------------------------------------------------------- clash flow

    /// <summary>Begins (or queues) a clash in a territory, instigated by a player.</summary>
    public void StartClash(string territoryId, string instigatorId)
    {
        if (State.ActiveClash is not null)
        {
            if (territoryId != State.ActiveClash.TerritoryId &&
                !State.ActiveClash.QueuedTerritories.Contains(territoryId))
                State.ActiveClash.QueuedTerritories.Add(territoryId);
            return;
        }
        var order = TurnOrderFrom(State.Players.FindIndex(p => p.PlayerId == instigatorId))
            .Select(s => State.Players[s].PlayerId).ToList();
        var clash = new ClashState { TerritoryId = territoryId, InstigatorId = instigatorId, Order = order };
        State.ActiveClash = clash;
        State.Phase = GamePhase.Clash;
        Emit("ClashStarted", instigatorId, TerritoryId: territoryId);

        var territory = State.Territories[territoryId];
        if (territory.HasFestival && !clash.FestivalApplied)
        {
            clash.FestivalApplied = true;
            RemoveClan(territory, State.PlayerById(instigatorId)!.Color);
        }

        // Warlord holders may join the fight before the Citadels step.
        if (TryOpenReactionWindow(new ReactionFrame
            {
                Trigger = ReactionTrigger.ClashStarted,
                TriggerPlayerId = instigatorId,
                TerritoryId = territoryId,
                Continuation = ReactionContinuation.BeginCitadelStep,
            }))
            return;
        BeginCitadelStep();
    }

    private void BeginCitadelStep()
    {
        var clash = State.ActiveClash!;
        clash.InResolution = false;
        clash.Cursor = 0;
        PromptNextShelter();
    }

    private void PromptNextShelter()
    {
        var clash = State.ActiveClash!;
        var territory = State.Territories[clash.TerritoryId];
        // Other players (not the instigator), in turn order, may shelter while citadels remain.
        while (clash.Cursor < clash.Order.Count)
        {
            var pid = clash.Order[clash.Cursor];
            if (pid != clash.InstigatorId && clash.ShelteredTotal < territory.TotalCitadels)
            {
                var pl = State.PlayerById(pid)!;
                var exposed = territory.ClansOf(pl.Color) - clash.Sheltered.GetValueOrDefault(pl.Color);
                if (exposed > 0)
                {
                    State.Pending = new PendingDecision { Kind = PendingKind.ClashShelter, PlayerId = pid };
                    return;
                }
            }
            clash.Cursor++;
        }
        BeginResolution();
    }

    private void ApplyShelter(Move move)
    {
        var clash = State.ActiveClash!;
        var territory = State.Territories[clash.TerritoryId];
        var player = State.PlayerById(move.PlayerId ?? State.Pending!.PlayerId)!;
        if (move.Type == MoveType.ClashShelter && clash.ShelteredTotal < territory.TotalCitadels)
        {
            clash.Sheltered[player.Color] = clash.Sheltered.GetValueOrDefault(player.Color) + 1;
            Emit("ClanSheltered", player.PlayerId, TerritoryId: territory.InstanceId);
            // The same player may shelter again if citadels and clans remain.
            PromptNextShelter();
        }
        else
        {
            clash.Cursor++;
            PromptNextShelter();
        }
    }

    private void BeginResolution()
    {
        var clash = State.ActiveClash!;
        clash.InResolution = true;
        clash.Cursor = 0;
        if (clash.ForcedFirstManeuverId is { } forced)
        {
            var idx = clash.Order.IndexOf(forced);
            if (idx >= 0) clash.Cursor = idx;
            clash.ForcedFirstManeuverId = null;
        }
        clash.AgreedToEnd.Clear();
        PromptNextManeuver();
    }

    private int Exposed(ClashState clash, ClanColor color)
    {
        var t = State.Territories[clash.TerritoryId];
        return t.ClansOf(color) - clash.Sheltered.GetValueOrDefault(color);
    }

    private void PromptNextManeuver()
    {
        var clash = State.ActiveClash!;
        // End the clash if no exposed clans remain at all.
        var anyExposed = State.Players.Any(p => Exposed(clash, p.Color) > 0);
        if (!anyExposed) { EndClash(); return; }

        // Find the next player (in order, cycling) who has exposed clans.
        for (var step = 0; step < clash.Order.Count; step++)
        {
            var idx = (clash.Cursor + step) % clash.Order.Count;
            var pid = clash.Order[idx];
            var pl = State.PlayerById(pid)!;
            if (Exposed(clash, pl.Color) > 0)
            {
                clash.Cursor = idx;
                State.Pending = new PendingDecision { Kind = PendingKind.ClashManeuver, PlayerId = pid };
                return;
            }
        }
        EndClash();
    }

    private void ApplyManeuver(Move move)
    {
        var clash = State.ActiveClash!;
        var player = State.PlayerById(move.PlayerId ?? State.Pending!.PlayerId)!;
        var territory = State.Territories[clash.TerritoryId];

        switch (move.Type)
        {
            case MoveType.EndClash:
                // A player may offer to end; the clash ends only if everyone with exposed clans agrees.
                clash.AgreedToEnd.Add(player.PlayerId);
                var involved = State.Players.Where(p => Exposed(clash, p.Color) > 0).Select(p => p.PlayerId);
                if (involved.All(clash.AgreedToEnd.Contains)) { EndClash(); return; }
                clash.Cursor = (clash.Cursor + 1) % clash.Order.Count;
                PromptNextManeuver();
                break;

            case MoveType.Attack:
            {
                var target = State.PlayerById(move.TargetPlayerId ?? "");
                Require(target is not null && Exposed(clash, target.Color) > 0, "Invalid Attack target.");
                clash.AgreedToEnd.Clear();
                clash.PendingAttackerId = player.PlayerId;
                clash.PendingTargetId = target!.PlayerId;
                Emit("Attack", player.PlayerId, TerritoryId: territory.InstanceId, Detail: target.PlayerId);
                // The attacked player must choose how to absorb it.
                if (HasPlayableActionCard(target))
                    State.Pending = new PendingDecision { Kind = PendingKind.AttackResponse, PlayerId = target.PlayerId };
                else
                {
                    RemoveClan(territory, target.Color);
                    clash.PendingAttackerId = null;
                    clash.PendingTargetId = null;
                    if (!TryOpenReactionWindow(new ReactionFrame
                        {
                            Trigger = ReactionTrigger.AttackResolved,
                            TriggerPlayerId = player.PlayerId,
                            TargetPlayerId = target.PlayerId,
                            TerritoryId = territory.InstanceId,
                            ClansRemoved = true,
                            Continuation = ReactionContinuation.AfterManeuver,
                        }))
                        AfterManeuver();
                }
                break;
            }

            case MoveType.Withdraw:
            {
                var dests = AdjacentChieftainTerritories(player, territory);
                Require(dests.Count > 0, "Cannot Withdraw: you are chieftain of no adjacent territory.");
                var to = Territory(move.ToTerritoryId) ?? dests[0];
                var amount = move.Amount > 0 ? move.Amount : Exposed(clash, player.Color);
                amount = Math.Min(amount, Exposed(clash, player.Color));
                territory.AddClans(player.Color, -amount);
                to.AddClans(player.Color, amount);
                Emit("Withdraw", player.PlayerId, TerritoryId: to.InstanceId, Detail: amount.ToString());
                AfterManeuver();
                break;
            }

            case MoveType.PlayCard:
            {
                // Maneuver-Triskels: played in place of a normal maneuver.
                var cid = move.CardId ?? throw new InvalidOperationException("PlayCard needs a card id.");
                Require(cid is TaleOfCuchulain or OgmasEloquence, "Only a maneuver Triskel may be played here.");
                Require(player.Hand.Contains(cid), "Card not in hand.");
                Require(!clash.TriskelsBlocked, "Lug's Spear blocks Triskel cards this clash.");
                player.Hand.Remove(cid);
                State.EpicDiscard.Add(cid);
                Emit("CardPlayed", player.PlayerId, cid);

                if (cid == OgmasEloquence) { EndClash(); return; }

                // Tale of Cuchulain: remove up to two exposed clans from the clashing territory.
                clash.AgreedToEnd.Clear();
                for (var i = 0; i < 2; i++)
                {
                    var victim = move.TargetColor is { } chosen && Exposed(clash, chosen) > 0
                        ? chosen
                        : State.Players.Select(p => p.Color)
                            .Where(c => c != player.Color && Exposed(clash, c) > 0)
                            .Cast<ClanColor?>().FirstOrDefault();
                    if (victim is not { } col) break;
                    RemoveClan(territory, col);
                }
                AfterManeuver();
                break;
            }

            default:
                throw new InvalidOperationException($"Illegal maneuver: {move.Type}.");
        }
    }

    private void ApplyAttackResponse(Move move)
    {
        var clash = State.ActiveClash!;
        var territory = State.Territories[clash.TerritoryId];
        var target = State.PlayerById(clash.PendingTargetId!)!;
        var attacker = clash.PendingAttackerId!;

        if (move.Type == MoveType.AttackDiscardCard)
        {
            var card = move.CardId ?? FirstActionCard(target);
            Require(card is not null && target.Hand.Contains(card), "No Action card to discard.");
            target.Hand.Remove(card!);
            State.ActionDiscard.Add(card!);
            Emit("DiscardedToAttack", target.PlayerId, card);
        }
        else // AttackRemoveClan (or default)
        {
            RemoveClan(territory, target.Color);
        }
        clash.PendingAttackerId = null;
        clash.PendingTargetId = null;

        // The attacker may follow up (Bard after removing a clan; Raid in any case).
        if (TryOpenReactionWindow(new ReactionFrame
            {
                Trigger = ReactionTrigger.AttackResolved,
                TriggerPlayerId = attacker,
                TargetPlayerId = target.PlayerId,
                TerritoryId = territory.InstanceId,
                ClansRemoved = move.Type != MoveType.AttackDiscardCard,
                Continuation = ReactionContinuation.AfterManeuver,
            }))
            return;
        AfterManeuver();
    }

    private void AfterManeuver()
    {
        var clash = State.ActiveClash!;
        clash.Cursor = (clash.Cursor + 1) % clash.Order.Count;
        PromptNextManeuver();
    }

    private void EndClash()
    {
        var clash = State.ActiveClash!;
        Emit("ClashEnded", TerritoryId: clash.TerritoryId);
        State.ActiveClash = null;

        // Resolve any queued clashes (e.g. from Migration) before returning to the Season.
        while (clash.QueuedTerritories.Count > 0)
        {
            var next = clash.QueuedTerritories[0];
            clash.QueuedTerritories.RemoveAt(0);
            var t = State.Territories[next];
            if (t.Clans.Count(kv => kv.Value > 0) > 1)
            {
                StartClash(next, clash.InstigatorId);
                return;
            }
        }

        State.Phase = GamePhase.Season;
        AdvanceSeasonTurn();
    }

    // ---------------------------------------------------------- legal moves

    /// <summary>The legal moves available to the pending player (menu-level; targets filled by caller).</summary>
    public IReadOnlyList<Move> LegalMoves()
    {
        var pending = State.Pending;
        if (pending is null) return Array.Empty<Move>();
        var player = State.PlayerById(pending.PlayerId)!;
        var list = new List<Move>();
        switch (pending.Kind)
        {
            case PendingKind.Draft:
                foreach (var c in State.Draft!.Hands[State.Players.IndexOf(player)].Distinct())
                    list.Add(new Move { Type = MoveType.DraftPick, PlayerId = player.PlayerId, CardId = c });
                break;

            case PendingKind.SeasonTurn:
                foreach (var c in player.Hand.Concat(player.Advantages).Distinct())
                    list.Add(new Move { Type = MoveType.PlayCard, PlayerId = player.PlayerId, CardId = c });
                if (!(player == State.Brenn && !State.BrennHasOpened))
                {
                    list.Add(Move.Pass(player.PlayerId));
                    if (!player.HasPretenderToken && State.PretendersRemaining > 0 && VictoryEvaluator.MeetsAny(State, player))
                        list.Add(new Move { Type = MoveType.TakePretender, PlayerId = player.PlayerId });
                }
                break;

            case PendingKind.ClashShelter:
                list.Add(new Move { Type = MoveType.ClashShelter, PlayerId = player.PlayerId });
                list.Add(new Move { Type = MoveType.ClashSkipShelter, PlayerId = player.PlayerId });
                break;

            case PendingKind.ClashManeuver:
                var clash = State.ActiveClash!;
                var territory = State.Territories[clash.TerritoryId];
                foreach (var opp in State.Players.Where(p => p != player && Exposed(clash, p.Color) > 0))
                    list.Add(new Move { Type = MoveType.Attack, PlayerId = player.PlayerId, TargetPlayerId = opp.PlayerId });
                if (AdjacentChieftainTerritories(player, territory).Count > 0)
                    list.Add(new Move { Type = MoveType.Withdraw, PlayerId = player.PlayerId });
                list.Add(new Move { Type = MoveType.EndClash, PlayerId = player.PlayerId });
                if (!clash.TriskelsBlocked)
                {
                    // Maneuver-Triskels are played as an extra kind of maneuver.
                    if (player.Hand.Contains(TaleOfCuchulain))
                        list.Add(new Move { Type = MoveType.PlayCard, PlayerId = player.PlayerId, CardId = TaleOfCuchulain });
                    if (player.Hand.Contains(OgmasEloquence))
                        list.Add(new Move { Type = MoveType.PlayCard, PlayerId = player.PlayerId, CardId = OgmasEloquence });
                }
                break;

            case PendingKind.AttackResponse:
                list.Add(new Move { Type = MoveType.AttackRemoveClan, PlayerId = player.PlayerId });
                if (HasPlayableActionCard(player))
                    list.Add(new Move { Type = MoveType.AttackDiscardCard, PlayerId = player.PlayerId });
                break;

            case PendingKind.Reaction:
            {
                var frame = State.ReactionStack[^1];
                foreach (var c in EligibleReactionCards(frame, player))
                    list.Add(new Move { Type = MoveType.PlayReaction, PlayerId = player.PlayerId, CardId = c });
                list.Add(new Move { Type = MoveType.PassReaction, PlayerId = player.PlayerId });
                break;
            }
        }
        return list;
    }

    // ----------------------------------------------------------- small helpers

    private List<TerritoryState> AdjacentChieftainTerritories(PlayerState player, TerritoryState from)
        => from.Adjacent.Select(id => State.Territories[id])
            .Where(t => t.Chieftain() == player.Color).ToList();

    private bool HasPlayableActionCard(PlayerState p)
        => p.Hand.Any(c => Data.TryGetCard(c, out var d) && d.Type == CardType.Action);

    private string? FirstActionCard(PlayerState p)
        => p.Hand.FirstOrDefault(c => Data.TryGetCard(c, out var d) && d.Type == CardType.Action);

    private int Direction() => State.Direction == TurnDirection.Clockwise ? 1 : -1;
    private static int Mod(int a, int n) => ((a % n) + n) % n;
    private int NextSeat(int seat) => Mod(seat + Direction(), State.Players.Count);

    /// <summary>Seat indices in turn order starting at <paramref name="startSeat"/>.</summary>
    private List<int> TurnOrderFrom(int startSeat)
    {
        var n = State.Players.Count;
        var order = new List<int>(n);
        var s = startSeat;
        for (var i = 0; i < n; i++) { order.Add(s); s = NextSeat(s); }
        return order;
    }

    private void Emit(string kind, string? playerId = null, string? cardId = null,
        string? TerritoryId = null, string? Detail = null)
        => _events.Add(new GameEvent(kind, playerId, cardId, TerritoryId, Detail));

    private static void Require(bool cond, string message)
    {
        if (!cond) throw new InvalidOperationException(message);
    }

    private void RequireActor(string actor, PendingDecision pending)
        => Require(actor == pending.PlayerId, $"It is not {actor}'s turn.");
}

internal static class ListExtensions
{
    public static string TakeFirst(this List<string> list)
    {
        var v = list[0];
        list.RemoveAt(0);
        return v;
    }
}
