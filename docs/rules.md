# INIS — rules transcription & implementation notes

This document tracks the canonical game content the engine implements. Card/tile
**structure** lives in `Inis.Core/Data/*.json`; this file records the authoritative
rules and the transcription status. All effect text in the data is **paraphrased in
our own words** (never the publisher's verbatim copy).

> Sources used: the official **Matagot 2016 rulebook PDF**, the **Seasons of Inis**
> rulebook PDF (EN + FR), the **Esoteric Order of Gamers** v2.2 reference, the
> **Inis Errata & FAQ**, **UltraBoardGames**, the **Inis Fandom wiki** (per-card
> pages), community card references, and **BoardGameGeek** (#155821, #255588).

## Players & components (base game, verified against rulebook)
- **2–4 players**, ~60 minutes (2–5 with *Seasons of Inis*).
- **16 Territory tiles**; **48 clan figures** (12 each in 4 colors).
- **20 buildings: 10 Citadels + 10 Sanctuaries.** One of the 10 Citadels is the
  larger **Capital**.
- **67 cards: 17 Action, 16 Advantage, 30 Epic Tale, 4 Reference.**
- Tokens: 1 Brenn, 1 Flock of Crows, 4 Pretender, 8 Deed, 1 Festival marker.

> **Action-card composition (verified against the official card list).** The **17
> base** Action cards are Bard, Citadel, Conquest, Craftsmen & Peasants, Druid,
> Emissaries, Exploration, Festival, Geis, Master Craftsman *(4-player)*, Migration,
> New Alliance, New Clans, Raid, Sanctuary, Scouts & Spies *(4-player)*, Warlord.
> Cards flagged `fourPlayerOnly` are removed below four players. *Seasons of Inis*
> adds the four 5th-player cards — **Clans Harmony, Coalition, Fili, The King and
> the Land** — and its updated **Exploration**/**Druid** replace the base versions,
> for 23 Action definitions total in `cards.json`.

## Victory (threshold 6; deeds = wild +1)
Three conditions, each met at value ≥ 6 (a Deed adds +1 to any one condition; a
single Deed can only complete one condition):
- **Leadership** — chieftain over a combined total of ≥6 opposing clans (sum, across
  territories you are chieftain of, of every clan that isn't yours).
- **Land** — present (≥1 clan) in ≥6 different territories.
- **Religion** — present in territories holding a combined ≥6 Sanctuaries.

**Adjudication (verified).** Checked **only** at Step 2 of the Assembly phase, and
**only** among players holding a **Pretender** token. The player meeting the **most**
conditions wins. On a tie for most: if the **Brenn** is among the tied players the
Brenn wins; otherwise there is no winner, all Pretender tokens are returned, and the
game continues. A player may hold at most one Pretender token (taken during a Season
turn when they meet ≥1 condition). The **Hy Brasil** advantage counts as one extra
Deed during this check.

## Round structure (verified)
1. **Assembly phase** — steps in order:
   1. **Assign Brenn** — the Capital territory's chieftain becomes Brenn. If the
      Capital has no chieftain, the Brenn is unchanged. (Only changes here.)
   2. **Check for victory** — see above; then return all Pretender tokens.
   3. **Take Advantage cards** — each territory's chieftain takes its Advantage card.
   4. **Flip the Flock of Crows** — tossed like a coin; sets turn-order direction.
   5. **Deal Action cards** — Brenn shuffles all Action cards, sets one aside
      facedown (**Cathbad's Word** lets its holder choose which), deals 4 to each
      player (3 in a 2-player game).
   6. **Action-card draft** — pick-and-pass: keep 1 of 4 / pass 3, then keep 2 / pass
      2, then keep 3 / pass 1, ending with 4 in hand. (2-player: two separate
      3-card drafts → 6 in hand.)
   7. **Sacred Festivals** (*Seasons of Inis* only) — see the season wheel below.
2. **Season phase** — the Brenn must open by playing a Season card (the Brenn may
   not pass on the very first turn). Then, in turn order, each player does one of:
   **play a Season card**, **pass**, or **take a Pretender token** (in Summer also
   the **Beltane move**, below). Ends when all players pass consecutively. Action
   cards are discarded; Epic Tales are kept; unplayed Advantage cards held by a
   non-chieftain are returned face-up.

A player who begins their turn with **no clans on the board** must first discard a
Deed (if any), then place 2 clans anywhere, then take their turn normally.

## Cards & timing (verified)
- **Season** cards are played on your turn; **Triskel** cards are played in response
  to a specific trigger. The engine models these as **reaction windows**: a trigger
  point opens a window only when an eligible holder exists; passing is always legal,
  so no seat can be stuck. Multiple Triskels answering the same trigger resolve
  active-player-first, then in Flock-of-Crows order (per the FAQ).
- Implemented triggers: Geis (opponent plays an Action card), The Dagda (anyone
  plays an Epic Tale/Advantage), Lug Samildanach (your card is cancelled by Geis),
  Master Craftsman (after your Epic Tale resolves), Warlord (a clash starts),
  Battle Frenzy (a clash's Citadels step ends), Bard / Raid / Streng's Resolve
  (after your Attack resolves), Dagda's Club / Diarmuid & Gráinne (an attack removed
  your clan), Dagda's Cauldron (a clash ends), Lug's Spear (suppresses further
  Triskels for the clash), Oengus's Ploy (any turn ends), Cathbad's Word (the
  Assembly's set-aside), and the maneuver-Triskels Tale of Cúchulain, Ogma's
  Eloquence, The Fianna.
- A clan that is **placed** never starts a clash; a clan that **moves** into a
  territory with opposing clans does. **Fili**'s token prevents any clash from
  starting in its territory until the season ends.

## Clash (combat) — verified
Triggered when clans move into a territory holding opposing clans, or by a card that
says so (the card names the **instigator**). Resolve immediately, in two steps:
1. **Citadels** — starting after the instigator, in turn order, each *other* player
   may shelter one clan per unoccupied Citadel (one clan per Citadel; the Capital's
   Citadel is just a Citadel). Sheltered clans are safe and uninvolved; everyone
   else's clans here are **exposed**. (If the Festival marker is here, the clash's
   initiator loses one clan before this step. Coalition partners may not shelter.)
2. **Resolution** — starting with the instigator (or Warlord's chosen player), in
   turn order, each player with exposed clans performs exactly one **maneuver**.
   Before each maneuver, the involved players may agree to end the clash. Maneuvers:
   - **Attack** — choose an opponent with exposed clans; they either discard an
     Action card (no effect) or remove one exposed clan. With no Action cards they
     must remove a clan. (Coalition partners cannot attack each other.)
   - **Withdraw** — move one or more of your exposed clans to adjacent territories
     where **you are chieftain** (does not start a new clash); not allowed if you are
     chieftain of no adjacent territory.
   - **Epic Tale maneuver** — play a Triskel Epic Tale "as a maneuver" (Tale of
     Cúchulain removes up to 2 exposed clans; Ogma's Eloquence ends the clash; The
     Fianna marches your clans, sheltered or exposed, to an adjacent territory).
The clash ends when no exposed clans remain or all involved agree to stop; sheltered
clans then leave their Citadels.

## Seasons of Inis (expansion modules)
- **5th player** — the 4 new Action cards, a 5th Pretender token and 12 white clans.
- **Season wheel** — a random starting season; the marker advances at each season's
  end. **Sacred Festivals** (Assembly step 7) and season modifiers:
  - **Spring (Imbolc)** — Assembly: the player(s) with the fewest cards in hand
    place one clan where present.
  - **Summer (Beltane)** — Season: instead of playing a card, a player may discard
    an Action card to move up to three clans (`SummerMove`).
  - **Autumn (Lugnasad)** — Assembly: each player may discard Epic Tales to place
    that many clans where present; afterwards no hand may keep more than three
    Epic Tales.
  - **Winter (Samhain)** — Assembly: each player may discard an Action card to draw
    an Epic Tale. Season: card-driven movement is limited to three clans.
- **Sea travel** — the Capital always has a **Harbour**; islands sit out at sea
  (never adjacent to anything) and carry a Harbour. When clans move, two territories
  with Harbours count as adjacent. Every movement legality check flows through one
  choke point (`GameEngine.AreConnected`), so sea routes apply to Conquest,
  Migration, Withdraw, Scouts & Spies, Emissaries, the Fianna, etc. uniformly.
- **New territories** — Aber, Hy Brasil, Isle of Joy, Inis Mona (verified names) and
  two provisional islands (Tir fo Thuinn, Mag Mell — `verified:false`), each with an
  Advantage card.

## Transcription status (each data row carries a `verified` flag)
- [x] **Territories:** all 16 base names verified; Seasons adds 6 (4 names verified,
  2 provisional islands).
- [x] **Action cards (23):** all names, set membership and effects verified,
  including the Seasons 5th-player cards and updated Exploration/Druid.
- [~] **Advantage cards (22):** texts verified for all 16 base cards and Aber /
  Hy Brasil; the remaining 4 island advantages are provisional. Engine handlers
  exist for Valley, Plains, Salt Mine, Misty Lands, Lost Vale, Aber and Hy Brasil
  (passive); Swamp is a rules-accurate no-op; the reactive/territory-effect
  modifiers (Meadows, Forest, Cove, Iron Mine, Stone Circle, Hills, Highlands,
  Mountains, Gates of Tír na nÓg, Moor) keep verified text but resolve as legal
  no-ops until their trigger systems are modeled.
- [x] **Epic Tales (30):** all names **and effects** verified and implemented —
  season-play effects through the effect registry, reactive ones through the
  reaction windows above.
- [x] Clash maneuver rules, setup, draft, victory adjudication — transcribed.
- [~] **Season wheel effects:** researched from the EN/FR rulebooks and reviews;
  Spring/Winter are well corroborated, Summer/Autumn rest on a single French
  source and are marked for a final card spot-check.

### Documented digital simplifications
- Reaction windows only open when an eligible holder exists, so being prompted
  reveals a Triskel is held (see `docs/protocol.md`).
- "Random" card takes (Raid, Salt Mine, Maeve's Wealth) draw deterministically from
  the seeded RNG or by stable hand order.
- The King and the Land's recipient always places their free clan (never declines);
  Coalition's partner chooses only how many clans to send.
- Dagda's Club implements its defensive mode (save the removed clan); the attacker's
  "choose the response" mode is not modeled.
- Diarmuid & Gráinne / Dagda's Club fire on attack removals (the dominant case),
  not on card-effect removals such as Balor's Eye.
- One-sea-move-per-card is relaxed: any single hop may be by sea.
