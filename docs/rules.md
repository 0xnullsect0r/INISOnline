# INIS — rules transcription & implementation notes

This document tracks the canonical game content the engine implements. Card/tile
**structure** lives in `Inis.Core/Data/*.json`; this file records the authoritative
rules and the transcription status. All effect text in the data is **paraphrased in
our own words** (never the publisher's verbatim copy).

> Sources used for this pass (egress now open): the official **Matagot 2016 rulebook
> PDF**, the **Seasons of Inis** rulebook PDF, the **Esoteric Order of Gamers** v2.2
> reference, the **Inis Errata & FAQ**, **UltraBoardGames**, the **Inis Fandom wiki**
> and **BoardGameGeek** (#155821).

## Players & components (base game, verified against rulebook)
- **2–4 players**, ~60 minutes. (Our engine also allows 5 seats for the later
  *Seasons of Inis* 5th-clan module; the base content is faithful to 2–4.)
- **16 Territory tiles**; **48 clan figures** (12 each in 4 colors).
- **20 buildings: 10 Citadels + 10 Sanctuaries.** One of the 10 Citadels is the
  larger **Capital**.
- **67 cards: 17 Action, 16 Advantage, 30 Epic Tale, 4 Reference.**
- Tokens: 1 Brenn, 1 Flock of Crows, 4 Pretender, 8 Deed, 1 Festival marker.

> **Action-card composition note.** Our `cards.json` holds **23** Action cards =
> the **17 base** cards **+ the *Seasons of Inis* expansion** Action cards (the 4
> new "5th-player" cards — Clans Harmony, Emissaries, Fili, Raid — plus the 2
> updated Exploration/Druid that replace the base versions). In a pure base game
> only the 17 base cards are used; the 4 cards flagged `fourPlayerOnly` are removed
> for games with fewer than four players.

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
turn when they meet ≥1 condition).

## Round structure (verified)
1. **Assembly phase** — six steps in order:
   1. **Assign Brenn** — the Capital territory's chieftain becomes Brenn. If the
      Capital has no chieftain, the Brenn is unchanged. (Only changes here.)
   2. **Check for victory** — see above; then return all Pretender tokens.
   3. **Take Advantage cards** — each territory's chieftain takes its Advantage card.
   4. **Flip the Flock of Crows** — tossed like a coin; sets turn-order direction.
   5. **Deal Action cards** — Brenn shuffles all Action cards, sets one aside
      facedown, deals 4 to each player (3 in a 2-player game).
   6. **Action-card draft** — pick-and-pass: keep 1 of 4 / pass 3, then keep 2 / pass
      2, then keep 3 / pass 1, ending with 4 in hand. (2-player: two separate
      3-card drafts → 6 in hand.)
2. **Season phase** — the Brenn must open by playing a Season card (the Brenn may
   not pass on the very first turn). Then, in turn order, each player does one of:
   **play a Season card**, **pass**, or **take a Pretender token**. Ends when all
   players pass consecutively. Action cards are discarded; Epic Tales are kept;
   unplayed Advantage cards held by a non-chieftain are returned face-up.

A player who begins their turn with **no clans on the board** must first discard a
Deed (if any), then place 2 clans anywhere, then take their turn normally.

## Cards & timing (verified)
- **Season** cards are played on your turn; **Triskel** cards are played in response
  to a specific trigger (e.g. Geis after an opponent's Action card; Warlord/Bard
  around clashes). Multiple Triskels can respond to the same trigger; the active
  player resolves first, then in Flock-of-Crows order.
- A clan that is **placed** never starts a clash; a clan that **moves** into a
  territory with opposing clans does.

## Clash (combat) — verified
Triggered when clans move into a territory holding opposing clans, or by a card that
says so (the card names the **instigator**). Resolve immediately, in two steps:
1. **Citadels** — starting after the instigator, in turn order, each *other* player
   may shelter one clan per unoccupied Citadel (one clan per Citadel; the Capital's
   Citadel is just a Citadel). Sheltered clans are safe and uninvolved; everyone
   else's clans here are **exposed**. (If the Festival marker is here, the clash's
   initiator loses one clan before this step.)
2. **Resolution** — starting with the instigator, in turn order, each player with
   exposed clans performs exactly one **maneuver** (no passing). Before each
   maneuver, the involved players may agree to end the clash. Maneuvers:
   - **Attack** — choose an opponent with exposed clans; they either discard an
     Action card (no effect) or remove one exposed clan. With no Action cards they
     must remove a clan.
   - **Withdraw** — move one or more of your exposed clans to adjacent territories
     where **you are chieftain** (does not start a new clash); not allowed if you are
     chieftain of no adjacent territory.
   - **Epic Tale maneuver** — play a Triskel Epic Tale "as a maneuver" (e.g. Tale of
     Cúchulain removes up to 2 exposed clans; Ogma's Eloquence ends the clash).
The clash ends when no exposed clans remain or all involved agree to stop; sheltered
clans then leave their Citadels.

## Transcription status (each data row carries a `verified` flag)
- [x] **Territories (16):** all names verified — Meadows, Forest, Mountains,
  Highlands, Misty Lands, Cove, Salt Mine, Iron Mine, Stone Circle, Swamp, Lost
  Vale, Gates of Tír na nÓg, Hills, Valley, Plains, Moor. (Replaced the earlier
  provisional "Wasteland"/"Shore" with the real **Plains**/**Moor**; no tile starts
  with a Sanctuary — that was also corrected.)
- [x] **Action cards (17 base):** all names verified. Effects verified for all
  except `Coalition` and `The King and the Land` (effect still provisional). The 6
  *Seasons of Inis* Action cards are present (names from BGG/EOG) with provisional
  effects (`verified:false`, `expansion` set).
- [~] **Advantage cards (16):** effects verified for Meadows, Forest, Highlands,
  Misty Lands, Cove, Salt Mine, Iron Mine, Stone Circle, Lost Vale, Hills, Moor.
  Still provisional: Mountains, Swamp, Gates of Tír na nÓg, Valley, Plains.
- [~] **Epic Tales (30):** all 30 real names verified (Diarmuid & Gráinne, Eriu,
  Battle Frenzy, Kernunos' Sanctuary, The Otherworld, Balor's Eye, The Battle of
  Moytura, Deirdre's Beauty, Tale of Cúchulain, Dagda's Club, Dagda's Harp, Lug's
  Spear, Dagda's Cauldron, Tuan's Memory, The Morrigan, Cathbad's Word, The
  Champion's Share, The Stone of Fal, Oengus's Ploy, Tailtu's Land, Breas' Tyranny,
  Ogma's Eloquence, Streng's Resolve, The Dagda, Manannan's Horses, Children of
  Dana, The Fianna, Maeve's Wealth, Lug Samildanach, Nuada Silverhand). Effects
  verified for Balor's Eye, The Battle of Moytura, Deirdre's Beauty, Tale of
  Cúchulain, Lug's Spear, The Morrigan, The Stone of Fal, Ogma's Eloquence, Lug
  Samildanach; the rest carry paraphrased provisional effects.
- [x] Clash maneuver rules — transcribed (above).
- [x] Setup / board layout / first Brenn / draft — transcribed (above).
- [x] Pretender/victory adjudication + Brenn tie-break — transcribed (above).

> Remaining `verified:false` rows are effect texts that still need a final card-image
> spot-check (the Esoteric Order summary and rulebook describe mechanics but not
> every individual Advantage/Epic Tale card verbatim). Names and counts are final.
