# INIS Online — implementation plan & progress

> This is the canonical plan, committed so any new session has full context.
> Companion docs: `docs/design.md` (UI/2.5D rendering), `docs/protocol.md`
> (netcode), `docs/rules.md` (rules transcription status), `docs/build.md`,
> `docs/credits.md`.

## Progress
- **Phase 0 (scaffolding): DONE** — solution (`Inis.slnx`), `Inis.Core` engine
  foundation, `Inis.Core.Tests`, `INISServer` (.NET 10: EF Core/Postgres, JWT
  access+refresh auth, friends, Scalar, game WebSocket stub, Dockerfile +
  docker-compose on :80), Godot 4.4 .NET client skeleton, CI workflow, docs.
  **Compiles** under .NET 8 (engine/tests) + .NET 10 (server) — fixed the missing
  `Microsoft.AspNetCore.OpenApi` package.
- **Phase 1 (assets + data): DONE** — content verified against the official
  Matagot 2016 rulebook, the Seasons of Inis rulebook, the Esoteric Order of
  Gamers v2.2 reference, the Inis errata/FAQ, UltraBoardGames and BGG. 16
  territories (all names verified; Plains/Moor replace the old provisional tiles),
  23 action cards (17 base + 6 Seasons of Inis), 16 advantages, 30 epic tales —
  all effect text paraphrased. Per-row `verified` flags + status in `docs/rules.md`.
  Art regenerated via `tools/gen-art.mjs` (85 SVGs; orphans pruned).
- **Phase 2 (core rules engine): DONE** — deterministic `GameEngine` (setup,
  six-step Assembly, pick-and-pass draft incl. 2-player, Season loop, clash
  resolution, pretender-gated victory + Brenn tie-break), one effect handler per
  card, legal-move API, debug command API, seeded replay. 56 xUnit tests green.
  Documented simplifications: Triskel reactive-window timing and a few
  Advantage/Epic effects are not yet fully modeled (see `docs/rules.md`).
- **Phase 4 (AI opponents): DONE** — deterministic `HeuristicAi` + `AiRunner`
  driving the public `LegalMoves`/`Apply` API (single-player, server seat-fill,
  engine fuzzing). AI-vs-AI soak tests play full games across seeds/player counts
  asserting no exceptions, an always-legal pending decision until game-over, and
  clan conservation every step, plus same-seed determinism and reachable winners.
  CI green (run #9).
- **Phase 5 (INISServer game sessions): DONE** — authoritative WebSocket sessions
  per `docs/protocol.md`. `GameSessionManager` (singleton) holds a `GameEngine` per
  game and rebuilds missing sessions from the database (deterministic resume via the
  persisted RNG cursor). `GameSession` is the single writer: maps intents→`Move`,
  applies, auto-plays AI seats via `HeuristicAi`, persists, and broadcasts per-player
  **redacted** `StateSync`/`Event`/`TurnPrompt`; reconnection replays StateSync;
  spectators and `DebugCommand` (server-authoritative, synced) supported. Lobbies
  (create/join by open seat or invite code, friend invite, AI fill, ready-up, start),
  shared `Inis.Core/Net` wire layer + redaction, EF migrations replacing
  `EnsureCreated()` with persisted games (jsonb). Server-reload determinism debt
  fixed. Integration tests (auth + friends + a scripted WebSocket bot playing a full
  game) wired into CI. CI green.
- **Phase 3 (Godot client UI shell + offline/hotseat): DONE** — code-built Celtic
  theme/design system, ScreenManager navigation, MainMenu/ModeSelect/GameSetup, and
  an in-game HUD that drives the embedded `Inis.Core` engine (legal-move buttons, AI
  auto-play, banners, action log). The 2.5D RISK-style board renders into a
  SubViewport: tilted orbit/pan/zoom camera, flat textured hex tiles, low-poly clan/
  building meshes, gold highlight, raycast tile picking → click-to-target card play.
  Validated by running Godot 4.4 headless (`scenes/SmokeTest.tscn`); CI does not
  build the Godot project.
- **Phase 6 (online multiplayer in client): DONE** — `IGameSource` lets one HUD run
  offline (`LocalGame`) or online (`RemoteGame`). Net layer: `Session`, `InisHttp`
  (REST auth/lobbies/friends), `RemoteGame` (ClientWebSocket reusing `Inis.Core/Net`,
  background receive + main-thread poll + auto-reconnect). AuthScreen, OnlineMenu and
  OnlineLobby cover login/register, create/join-by-code, ready-up, host AI-fill and
  start. Validated end-to-end against a live server (Postgres + INISServer) by a
  headless OnlineSmoke playing a full game to GameOver.
- **Phase 7 (LAN multiplayer): DONE** — the client hosts an authoritative session
  (`LanHost`) speaking the same WebSocket protocol as the online server; peers join
  with a `Join` handshake (`LanClientGame`), AI fills empty seats, and the host plays
  in-process (`LanHostGame`). UDP-broadcast discovery (`LanDiscovery`). One shared
  client base (`WsGameSourceBase`) backs both online and LAN. Loopback-validated.
- **Phase 8 (settings, audio, Debug screen): DONE** — `Settings` (persisted to
  `user://settings.cfg`, applied live to the audio buses + window), `AudioManager`
  autoload (pooled SFX + looping ambient music) with original audio synthesized by
  `tools/gen-audio.py`, a tabbed `SettingsPanel`, and the gated Debug/Cheat panel
  (code `INIS`) that grants cards / sets deeds via the server-authoritative
  `DebugCommand` path in every mode.
- **Phase 9 (cross-platform export & packaging): DONE** — `game/export_presets.cfg`
  for Windows/macOS/Linux/Android/iOS and a committed `game/INISOnline.sln` (the
  .NET export requires it). Per-build server endpoint via `application/config/server_url`.
  The headless Linux export produces a standalone binary that boots and links the
  engine; other targets need their platform SDKs/signing. See docs/build.md.
- **Phase 10 (Seasons of Inis): DONE (toggle).** `GameOptions.SeasonsOfInis` selects
  the content set at `GameEngine.Create` (persisted on `GameState`): base = 2–4 + 17
  action cards; Seasons = 2–5 (5th clan) + the expansion action cards, with the
  updated exploration/druid variants replacing the base ones. Threaded through
  offline/hotseat, the server lobby, and the LAN host. The season board (summer/
  winter modifiers), harbours and sea travel remain a later slice.
- **Phase 11 (6–8 player extended mode): DONE** — `GameOptions.Extended` raises the
  seat cap to 8, adds three more clan colours (Purple/Orange/Teal) and doubles the
  action deck so the larger draft has enough cards (house-ruled, non-official).
  Threaded through offline/hotseat, the server lobby and the LAN host.
- **Phase 12 (release CI/CD): DONE** — `.github/workflows/release.yml` triggers on
  `v*` tags: builds the client exports (desktop unconditionally; macOS/iOS and
  Android gated behind `ENABLE_APPLE_BUILDS` / `ENABLE_ANDROID_BUILDS` repo vars +
  signing secrets), builds & pushes the `INISServer` image to GHCR, and publishes a
  GitHub Release with the artifacts. `ci.yml` remains the per-push build/test gate.

**All 12 phases complete.** Remaining work: the deeper *Seasons of Inis* subsystems
(season board, harbours, sea travel), fleshing out the documented engine
simplifications, real signing material for the mobile/desktop installers, and
playtest polish.

## Environment notes
- `.NET 10 SDK` and `Godot` are NOT preinstalled; install the SDK in a fresh
  session (egress to `builds.dotnet.microsoft.com` now allowed). NuGet is reachable.
- Nothing has been compiled yet — author carefully; CI compiles/tests on push.
- Branch: `claude/confident-lovelace-fclngx`. Push there only. Do not open a PR
  unless asked.

---

# INIS — Digital Adaptation (Godot + ASP.NET multiplayer server)

## Context

The user wants a faithful cross-platform digital adaptation of **INIS** (Christian
Martinez, Matagot, 2016) — a Celtic-mythology card-drafting / area-control game —
playable on macOS, Windows, Linux, Android and iOS, built in **Godot**. It must
support online multiplayer through a single **centralized authoritative server**
(`INISServer`, ASP.NET Core / .NET 10, Scalar docs, one `docker compose`, served
on port 80 behind a reverse proxy at `inis.aricummings.com`), with **authenticated**
accounts (username/password) and friend requests. No progression/leveling.

The repo (`INISOnline`) is currently empty except for `LICENSE`. Greenfield build.

**Decisions confirmed with the user:**
- Build the **base 2016 game faithfully for 2–5 players first**. *Seasons of Inis*
  is a **later phase**, then implemented fully. 6–8 players deferred (published game
  caps at 4, or 5 with Seasons).
- **Shared C# rules engine** used by both the Godot client and the server → offline
  play AND server-authoritative online with zero duplicated logic.
- Game modes: **online multiplayer, local hotseat, LAN multiplayer, single-player
  vs AI**.
- **UI styled like Catan Universe / "Catan Online"**: clean, modern, friendly,
  animated, with a polished main menu, lobby, and in-game HUD.
- **In-game settings menu** (music/SFX volumes, etc.) and **background music +
  full SFX** (royalty-free) — added in a **later phase**.
- A gated **Debug/Cheat screen**: in-game settings icon → "Debug Code" → enter
  `INIS` → cheat panel to view/edit your hand and grant yourself Action/Epic Tale
  cards.

**Chosen defaults (standard, reversible):**
- **Godot 4.4+ .NET/C# build** (C# client references the shared engine; .NET mobile
  export supported in Godot 4.2+).
- **WebSocket + JSON** transport (Godot native `WebSocketPeer`; ASP.NET WebSockets).
  Server-authoritative; clients send intents, receive authoritative diffs/events.
- **PostgreSQL + EF Core**, **JWT (access + refresh)** auth, ASP.NET Identity
  password hashing.
- **Original SVG-authored Celtic art** (not Matagot's exact art; user approved).
  All the same tiles/cards/tokens/pieces present.
- LAN/offline reuse the engine via an **embedded headless host** in the client,
  discovered over UDP broadcast — one networking codepath.

---

## Game rules reference (what the engine must model)

**Goal:** First to meet a victory condition at the start of an Assembly Phase (and
not be blocked by a rival pretender) wins. Three conditions, threshold 6 (deeds =
wild +1):
- **Leadership** — chieftain in territories together holding ≥6 of opponents' clans.
- **Land** — present in ≥6 different territories.
- **Religion** — present in territories containing ≥6 sanctuaries.

**Round = Assembly Phase then Season Phase.**

*Assembly:* (1) victory check / pretenders → (2) chieftains per territory + set the
**Brenn** (chieftain of Capital's territory; else carried/passed) → (3) **pick-and-
pass Action-card draft** until each player holds 4.

*Season:* from the Brenn, clockwise, each turn **one** of: **play a card**, **pass**,
or **take a Pretender token**. Ends when all pass consecutively; Action cards
discarded, Epic Tales kept.

**Clash:** opposing clans in a territory + a trigger → players alternate maneuvers
(attack/withdraw/gift-for-deed …) until all pass; sanctuaries restrain clashes.
(Exact maneuvers transcribed from rulebook in implementation.)

**Base components → modeled & built as assets:** 16 hex **Territory tiles**; 48
**Clan figures** (4×12); 18 **Buildings** (9 Sanctuary, 9 Citadel incl. 1
**Capital**); 74 **Cards** — 30 **Epic Tale**, 23 **Action** (Battle/Clash, Warlord,
Conquest, Migration, Exploration, Citadel, Sanctuary, Bard, Druid, Festival, New
Alliance, Trade, Sage, etc. — exact list + counts transcribed), 16 **Advantage**,
5 **Reference**; tokens: **Brenn**, **Pretender**, **Deed**, **Trigger/Epic**.

> All cards/tiles are **data-driven** (JSON resources consumed by engine + asset
> pipeline) so published wording/quantities are transcribed once into data.

Research sources: BoardGameGeek (Inis #155821), UltraBoardGames, Order of Gamers
v2.2, Matagot rulebook, Inis Fandom wiki, Inis errata/FAQ.

---

## Architecture

### Layered, shared-engine design

```
INISOnline/
  Inis.Core/            # .NET class library — the ONLY place rules live.
    Model/              #   State: TerritoryGraph, Clan, Building, Deck, Hand,
                        #     Deed, Brenn/Pretender, SeasonTrack.
    Data/               #   Card/tile definitions loaded from JSON.
    Rules/              #   Phase machine, draft, clash, victory eval.
    Effects/            #   One handler per card effect (registry).
    Moves/              #   Legal-move generation (drives UI + AI).
    Ai/                 #   Heuristic AI seats.
    Debug/              #   Cheat/debug command API (grant/edit/remove cards).
    Net/                #   Shared message DTOs + (de)serialization.
  Inis.Core.Tests/      # xUnit
  game/                 # Godot 4.4 .NET client (references Inis.Core)
  INISServer/           # ASP.NET Core .NET 10 (references Inis.Core); own compose
  assets/               # Source SVGs + export pipeline
  docs/                 # Rules transcription, protocol spec, design, build docs
  Inis.sln
```

**Authority model.** `Inis.Core` is deterministic (seeded RNG) and side-effect-free
w.r.t. I/O. Online: the **server** owns the canonical instance; clients send
**intents** and render authoritative **diffs** — clients cannot fabricate state.
Offline/hotseat/LAN: the **client hosts** an embedded instance of the exact same
engine and speaks the exact same WebSocket protocol to itself / LAN peers.

**Protocol (`docs/protocol.md`).**
- Client→server intents: `DraftPick`, `PlayCard`, `Pass`, `TakePretender`,
  `ClashManeuver`, `ChooseTarget`, `Resign`, `Chat`, plus `DebugCommand` (gated).
- Server→client: `StateSync` (full), `Diff` (delta), `Event` (animatable: card
  played, clash hit, building placed…), `TurnPrompt` (whose turn + legal options),
  `Error`. Each message versioned; shared DTOs prevent client/server drift.
- **Reconnection:** on reconnect the server replays `StateSync` + pending prompt.
- **Per-player views:** server emits redacted state (opponents' hands hidden);
  hidden info never leaves the server in online play (true anti-cheat boundary).

**Determinism & replays:** seeded RNG + ordered intent log ⇒ full game replay from
a seed (used in tests and bug repro).

---

## UI / UX design (Catan-Universe-inspired)

### Rendering: 2.5D (RISK-style)
The board is a **3D scene with a tilted camera** (flat board seen at ~50–60°) and
**3D pieces standing on it**. Hex tiles are flat textured meshes (SVG tile textures);
clan figures / sanctuaries / citadels / capital are **low-poly 3D meshes** (per-player
color, shadows, drop/march/fall animations). Cards, hand, HUD and menus are **crisp 2D
overlays** on a `CanvasLayer`. The 2D piece SVGs double as UI icons + a billboard
fallback. Orbit/pan/zoom (drag, pinch). See `docs/design.md`.

### Design system (`game/ui/theme/`)
- **Visual language:** warm parchment + slate Celtic palette, gold/bronze accents,
  knotwork borders, soft drop shadows, rounded panels, large tactile buttons,
  smooth tweened transitions — the friendly, premium feel of Catan Universe.
- **Godot `Theme` resource** centralizes fonts, colors, button/panel/slider styles,
  focus states. One theme drives every screen for consistency.
- **Reusable components:** `PrimaryButton`, `IconButton`, `Card` (hover-zoom, drag),
  `PlayerBanner` (avatar, color, clan count, chieftain/Brenn badges), `Modal`,
  `Toast`, `Tooltip`, `Slider`, `Tabs`, `ConfirmDialog`.
- **Responsive layout:** anchor/container-based; desktop (mouse, hover tooltips,
  hotkeys) and touch (larger hit targets, long-press for card detail, pinch-zoom
  board) variants. Safe-area handling for iOS notch.

### Screen map / scene tree
- **Boot / loading** → **Main Menu** (Play, Multiplayer, Settings, Quit; animated
  background, music starts).
- **Account** (online): login / register, with validation + error toasts.
- **Mode select:** Single-player (vs AI), Local Hotseat, LAN (host/join+discovery),
  Online.
- **Friends** panel: list, online status, send/accept/decline requests, invite to
  lobby.
- **Lobby:** player slots (humans + AI fill), color pick, ready-up, rules options
  (player count 2–5; later: expansion/debug toggles), invite code, chat.
- **Game (HUD):** hex **board** (pan/zoom), **hand** dock (fan, drag-to-play),
  **draft** overlay, **clash** panel, **player banners** rail, **phase/turn**
  indicator + timer, **action log**, **chat**, **settings (gear) icon**, victory-
  progress trackers (Leadership/Land/Religion pips), end-game results screen.
- **Settings** (overlay, reachable from menu and in-game gear) and **Debug** screen
  (below).

### In-game settings menu (gear icon → modal, tabbed)
- **Audio:** Master / Music / SFX / UI volume sliders (live preview), mute toggle.
- **Video:** fullscreen/windowed, resolution, V-sync, animation speed, UI scale,
  colorblind-friendly clan palette.
- **Gameplay:** confirm-before-commit, auto-pass when no legal move, tooltip detail
  level, turn-timer display.
- **Account/Session (online):** profile, friends shortcut, leave/resign game.
- **Debug Code** button (always present here) → opens code entry.
- Settings persist to `user://settings.cfg`; audio reads/writes the audio bus
  volumes immediately.

### Debug / Cheat screen (gated)
- Flow: **gear icon → "Debug Code" → text box → enter `INIS`** → unlock **Cheat
  panel** for the current game.
- Capabilities (via `Inis.Core/Debug` command API):
  - **View** your full hand (and optionally face-up game state).
  - **Edit** a held card (swap for another definition).
  - **Grant** yourself a new **Action** card or **Epic Tale** card (picker lists all
    card definitions from data).
  - (Stretch) adjust your deeds / spawn a clan for testing.
- **Works everywhere, including real online games (per user).** The code `INIS`
  unlocks the cheat UI; the chosen cheat is sent to the server as a `DebugCommand`
  intent. The **server applies it to the authoritative game state** through
  `Inis.Core/Debug`, **recognizes the change** as canonical, and **broadcasts the
  resulting `Diff`/`Event`** to all clients so everyone stays in sync (the cheating
  player's hand updates; hidden-info redaction still hides the actual card faces
  from opponents, but the authoritative state is genuinely changed).
- **Sync guarantees:** the debug mutation goes through the same intent→validate→
  mutate→broadcast pipeline as normal moves (server-authoritative, deterministic),
  so server state, the acting client, and all other clients/spectators converge.
  `DebugCommand` is server-logged (audit) but **not blocked** in online play.

---

## Audio (later phase)

- **Audio buses:** `Master → {Music, SFX, UI}`; sliders in Settings map to bus
  volumes; persisted.
- **Music:** loopable **royalty-free Celtic** tracks (sources: Kevin MacLeod /
  incompetech, FreePD, Pixabay Music, OpenGameArt — all CC0/CC-BY with attribution
  recorded in `docs/credits.md`); separate menu vs in-game ambience, crossfaded.
- **SFX:** card draw/play, drag pickup/drop, building placed, clash hit/clan
  removed, draft pick, turn-start chime, victory fanfare, error buzz, toast, and a
  **menu button-click** sound. Centralized `AudioManager` autoload with pooled
  players; events emitted by the engine's `Event` stream and by UI components.

---

## Authentication & security (server)

- **Register/login** with username + password; **ASP.NET Identity** password
  hashing; password strength rules.
- **JWT access tokens** (short-lived) + **refresh tokens** (rotating, revocable in
  DB). All REST endpoints except register/login require a valid bearer token.
- **WebSocket auth:** token presented on connect; socket bound to the authenticated
  user; intents validated against that user's seat (can't act for others).
- **Authorization:** friend/lobby/game actions checked against the caller's
  identity; per-player redacted state as above.
- **Hardening:** rate limiting on auth endpoints, input validation, CORS for the
  app origin, HTTPS terminated at the upstream reverse proxy (server itself on
  port 80), secrets via env vars, audit logging of auth events.

---

## Implementation phases

### Phase 0 — Scaffolding
Solution, `Inis.Core` (+tests), `INISServer` (.NET 10), Godot 4.4 .NET project, CI
(build/test engine + Godot export check), `.gitignore`, README, `docs/` skeleton
(protocol + design specs).

### Phase 1 — Assets
SVG tiles/figures/buildings/cards/tokens + UI chrome (Catan-like); SVG→PNG/atlas
export pipeline; transcribe `docs/cards.json` / `tiles.json` (name, type, count,
cost, effect) — canonical data for engine + art. (Flag for user spot-check.)

### Phase 2 — Core rules engine (base game)
State model, data loader, Assembly/Season phase machine, card-effect registry,
clash, victory eval (deeds-as-wild), legal-move API, debug command API, seeded RNG.
Exhaustive unit tests (each card, draft, clash, all 3 win paths, replays).

### Phase 3 — Godot client: UI shell + offline/hotseat
Theme/design system + reusable components; main menu, mode select, game HUD, hand,
draft, clash UI. **2.5D board:** tilted 3D scene, hex tiles as flat textured meshes,
**low-poly 3D piece meshes** (clans/buildings) with drop/march/fall animations,
orbit/pan/zoom camera; 2D card/HUD overlays. Drive games via the embedded engine
(hotseat + single device) to validate the engine through real play.

### Phase 4 — AI opponents
Heuristic/utility AI in `Inis.Core` (draft, play, clash, pretender timing),
difficulty tiers; used for single-player and seat-fill.

### Phase 5 — INISServer (ASP.NET, .NET 10)
Auth (JWT + refresh, Identity hashing), friends, lobbies/matchmaking (2–5 + AI
fill), authoritative WebSocket game sessions (validation, redacted broadcast,
reconnection, spectators, turn timers), **Scalar** OpenAPI UI, PostgreSQL/EF Core,
**`docker compose`** (`server` + `postgres` + migrations) on **port 80**, health
checks, env config. Kept extraction-ready for its own repo/submodule.

### Phase 6 — Online multiplayer in client
WebSocket net client, auth/login + register UI, friends UI, lobby browser/invites,
in-game sync, reconnection, spectate.

### Phase 7 — LAN multiplayer
Client-hosted embedded session + UDP-broadcast discovery; peers join via the same
protocol.

### Phase 8 — Settings, audio, polish & Debug screen
Full Settings menu (audio/video/gameplay/account tabs), `AudioManager` + buses,
royalty-free music + complete SFX set + menu click, animations/transitions polish,
and the gated **Debug/Cheat** screen (code `INIS`). (Per user: audio/settings are a
later phase.)

### Phase 9 — Cross-platform export & packaging
Export presets + docs for macOS/Windows/Linux/Android/iOS (.NET mobile export,
signing); configurable server endpoint per build; smoke-test each target.

### Phase 10 (later) — *Seasons of Inis* expansion
Modular toggles: season board + summer/winter modifiers, harbor tiles & sea travel,
5th clan, new territories/Action/Epic Tale cards — added as data + new effect
handlers + lobby options.

### Phase 11 (later) — 6–8 player extended mode
House-ruled scaling (tiles, clan counts, deck copies, victory thresholds) after the
faithful game + Seasons are solid.

### Phase 12 — Release CI/CD (installers + server image)
GitHub Actions release pipeline triggered on version tags (`v*`). Builds and
publishes signed/distributable artifacts for every platform and attaches them to a
GitHub Release:
- **Windows:** `.msi` installer (e.g. WiX/`dotnet`-built MSI around the Godot export).
- **macOS:** `.dmg` (Godot `.app` export, codesign + notarize on a macOS runner).
- **Linux:** **Flatpak** (`.flatpak`/repo) and **AppImage**.
- **Android:** `.apk` (and `.aab`), keystore-signed.
- **iOS:** `.ipa` via Xcode build of the Godot iOS export on a macOS runner, Apple
  signing.
- **Server:** build + push the `INISServer` Docker image to a registry (tagged).
Headless **Godot export templates** are installed in CI; signing material
(keystores, Apple/Windows certs) supplied via encrypted **Action secrets**. Mobile
builds need macOS runners (iOS) and the .NET mobile workload. CI (build+test) from
Phase 0 remains the per-push gate; this phase adds the tag-driven release job.

---

## Server API detail (Phase 5)

- **REST (Scalar):** `POST /auth/register`, `POST /auth/login`,
  `POST /auth/refresh`, `GET /me`; friends `GET /friends`, `POST /friends/requests`,
  `PUT /friends/requests/{id}` (accept/decline), `DELETE /friends/{id}`; lobbies
  `GET/POST /lobbies`, `POST /lobbies/{id}/join|leave|ready|start`; `GET /games/{id}`.
- **WS** `/ws/game/{id}` (token-authed): intents + `StateSync/Diff/Event/
  TurnPrompt/Error` as in the protocol.
- **DB:** `users`, `refresh_tokens`, `friendships(requester, addressee, status)`,
  `lobbies`, `games(state jsonb, players, status, seed)`.

---

## Verification

- **Engine:** `dotnet test` — per-card effects, draft, clash, all win paths,
  deterministic replay from seed.
- **Server:** integration tests for auth + refresh + friends + lobby; a scripted
  WebSocket bot playing a full game; `docker compose up` → Scalar reachable → smoke
  game; verify `DebugCommand` in an online game mutates authoritative server state
  and broadcasts a synced `Diff` to all connected clients.
- **Client:** full hotseat game; vs-AI game; 2-client online game vs dockerized
  server; LAN discovery + join; settings persistence + live audio volume; Debug
  code `INIS` unlock + grant/edit card in both a local and an online game (online
  change confirmed on the server and synced to a second client).
- **Cross-platform:** export and launch on all 5 targets.

## Open considerations
- Exact card quantities/wording transcribed into `docs/*.json` in Phase 1 (user
  spot-check).
- Final royalty-free music tracks chosen in Phase 8; attributions in
  `docs/credits.md`.
- Whether to later split `INISServer`/`Inis.Core` into their own repos/submodules —
  kept as extraction-ready folders since this session can only push to `INISOnline`.


---

## v1.1 finish pass (post-Phase-12)

A comprehensive audit + completion pass (branch `claude/finish-project`), in seven
milestones — see `transfer.md` §11 for the summary and `docs/rules.md` /
`docs/protocol.md` for the resulting canon:

- **M0** repo hygiene (`.gitattributes`, keystore ignore rules, dependency pins).
- **M1** release pipeline fixes (GHCR lowercase, Android build template + editor
  settings, job gating, dispatch dry-runs) — verified with a pre-release tag before
  cutting `v1.1`.
- **M2** server hardening: rate limits, CORS scoping, auth audit logs, JWT
  fail-fast, background lobby/session sweeper, compose secret requirements.
- **M3** rules fidelity: face-up Advantage zone; the reaction (Triskel) window
  framework with persisted continuations; every Action and Epic Tale card
  researched, paraphrased, implemented and tested; card-set membership corrected.
- **M4** full *Seasons of Inis*: season wheel + Sacred Festivals, seasonal
  modifiers, harbours/sea travel through the `AreConnected` choke point, islands
  and the six new territories/advantages.
- **M5** protocol v2 with handshake enforcement; Triskel-stacked AI soak; docs.
- **M6** client UX: card-art hand dock, reaction prompts, clash panel, victory
  progress, chat, reconnect backoff, board fixes (mid-game tiles, harbours,
  piece pop-in), audio crossfade, V-Sync/UI-scale/colorblind settings.
- **M7** docs + final verification (149 engine / 9 server tests, headless client
  smokes) and the `v1.1` release.
