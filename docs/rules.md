# INIS — rules transcription & implementation notes

This document tracks the canonical game content the engine implements. Card/tile
**structure** lives in `Inis.Core/Data/*.json`; this file records the authoritative
rules and the transcription status. Anything marked **PROVISIONAL** must be verified
against the official rulebook and is flagged for user spot-check (Phase 1).

## Victory (threshold 6; deeds = wild +1)
- **Leadership** — chieftain in territories together holding ≥6 of opponents' clans.
- **Land** — present in ≥6 different territories.
- **Religion** — present in territories containing ≥6 sanctuaries.

Win is adjudicated at the **start of the Assembly phase**, mediated by Pretender
tokens (a player must "pretend" during the Season to be eligible, and is blocked if
a rival also qualifies). Exact pretender/tie adjudication: **to transcribe**.

## Round structure
1. **Assembly phase**
   - Victory check / resolve pretenders.
   - Determine chieftains per territory (most clans; ties → none / tie-break **TBD**).
   - Set the **Brenn** (chieftain of the Capital's territory; else carried/passed).
   - **Action-card draft**: pick-and-pass starting with the Brenn until each player
     holds 4 Action cards.
2. **Season phase** — from the Brenn, clockwise; each turn: play a card / pass /
   take a pretender token. Ends on consecutive passes by all. Discard Action cards,
   keep Epic Tales.

## Clash (combat) — **PROVISIONAL, to transcribe**
Triggered by certain cards when opposing clans share a territory. Players alternate
maneuvers (attack = remove an opposing clan; withdraw; gift a card for a deed; …)
until all pass. Sanctuaries restrain clashes. Exact maneuver list + sequencing TBD.

## Components (base game)
- 16 territory tiles — see `territories.json` (names **PROVISIONAL**).
- 48 clan figures (4 colors × 12).
- 9 Sanctuaries, 9 Citadels (1 is the Capital).
- 74 cards: 30 Epic Tale, 23 Action (17 types), 16 Advantage, 5 Reference.
- Tokens: Brenn, Pretender, Deed, Trigger/Epic.

## Transcription checklist
- [ ] Confirm 16 territory names + terrain + advantage links.
- [ ] Confirm 17 action card types, exact text, and counts (sum = 23).
- [ ] Transcribe all 30 Epic Tale cards.
- [ ] Transcribe all 16 Advantage cards.
- [ ] Transcribe clash maneuver rules.
- [ ] Confirm setup (board layout per player count, starting clans, first Brenn).
- [ ] Confirm pretender/victory adjudication + tie-breaks.

## Sources
BoardGameGeek (Inis #155821), UltraBoardGames rules, Order of Gamers reference
v2.2, Matagot rulebook, Inis Fandom wiki, Inis errata/FAQ.
