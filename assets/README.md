# Assets

All art is **original** (Celtic-styled SVG), not Matagot's. **Godot 4 imports SVG
natively** (rasterizing on import with a configurable scale), so there is **no
external rasterizer step** — SVGs are placed directly under `game/art/` and consumed
by the client.

## Where art lives
```
game/art/
  cards/{action,epic,advantage}/  # one SVG per card (generated)
  cards/back/card_back.svg
  tiles/                          # one SVG per territory tile (generated)
  pieces/                         # clan_{color}, sanctuary, citadel, capital
  tokens/                         # brenn, pretender, deed, trigger
  ui/                             # button, panel, gear
  board/                          # board_bg
```

## Generated vs authored
- **Cards and tiles are generated** from the canonical catalogue
  (`Inis.Core/Data/cards.json`, `territories.json`) by `tools/gen-art.mjs`, so art
  and rules never drift. Provisional (unverified) cards are watermarked.
  Regenerate after editing the data:
  ```bash
  node tools/gen-art.mjs
  ```
- **Pieces, tokens, UI and board** are hand-authored SVGs under `game/art/`.

File ids match data ids (e.g. `cards/action/battle.svg` ↔ `action.battle`).

This `assets/` folder is reserved for higher-fidelity source art (e.g. layered
illustrations) added later; the shipping art currently lives under `game/art/`.
