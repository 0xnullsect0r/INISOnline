# Assets pipeline

Source art is authored as **SVG** here, then exported to PNG/atlases consumed by the
Godot client. Art is **original** (Celtic-styled), not Matagot's.

## Layout
```
assets/
  svg/
    tiles/        # 16 territory tiles (hex)
    cards/
      action/     # action card faces
      epic/       # epic tale faces
      advantage/  # advantage faces
      back/       # card backs
    pieces/       # clan figures, sanctuary, citadel, capital
    tokens/       # brenn, pretender, deed, trigger
    ui/           # buttons, panels, knotwork frames, icons
  export/         # generated PNG/atlas output (gitignored)
```

Card faces are generated from templates + the canonical text in
`Inis.Core/Data/cards.json`, so art and rules never drift.

## Export
`tools/export-assets.sh` rasterizes SVG → PNG at target DPIs into `export/` and into
the Godot import dirs. (Phase 1 wires this up; requires `rsvg-convert` or `inkscape`.)

## Naming
File ids match the data ids, e.g. `cards/action/clash.png` ↔ `action.clash`.
