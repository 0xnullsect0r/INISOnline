# INIS Online — Session Transfer / Handoff

This file is the single source of truth for picking up the project in a new
session. Read it first, then `docs/plan.md` (full plan), `docs/rules.md`
(rules transcription + status), `docs/design.md` (UI + 2.5D rendering), and
`docs/protocol.md` (netcode).

---

## 1. What we're building

A faithful cross-platform digital adaptation of **INIS** (Christian Martinez,
Matagot, 2016) — a Celtic-mythology card-drafting / area-control board game —
in **Godot 4.4 (.NET/C#)**, with a centralized authoritative **ASP.NET Core
(.NET 10)** multiplayer server. Targets: **macOS, Windows, Linux, Android, iOS**.

It's a fan project: **original SVG art** (not Matagot's) and **paraphrased** rules
text (never the publisher's verbatim wording). Keep it that way.

## 2. Confirmed decisions (Q&A answers — do not re-litigate)

- **Players:** build the base 2016 game **faithfully for 2–5** first. **6–8** is
  deferred to Phase 11 (not an official config). Engine currently allows 2–5.
- **Expansions:** base game first; ***Seasons of Inis*** is **Phase 10** (then
  implement it fully). Data already includes the 6 Seasons action cards (flagged).
- **Architecture:** one **shared C# rules engine** (`Inis.Core`) used by BOTH the
  Godot client (offline/hotseat/LAN host) AND the server (authoritative online).
  Never duplicate rules.
- **Game modes:** online multiplayer, local hotseat, LAN, single-player vs AI.
- **Rendering: 2.5D, like digital RISK** — a flat board viewed from a tilted 3D
  camera with **low-poly 3D pieces** (clans/buildings) standing on it; cards/HUD/
  menus are crisp **2D overlays**. UI styled like **Catan Universe** (warm
  parchment/slate Celtic theme). See `docs/design.md`.
- **Settings menu + audio** (royalty-free Celtic music + full SFX + menu click)
  are a **later phase (Phase 8)**.
- **Debug/Cheat screen:** gear → "Debug Code" → enter **`INIS`** → view/edit hand,
  grant Action/Epic cards, etc. It **works in real online games**: the command is
  sent as a `DebugCommand`, applied **authoritatively by the server**, and the
  synced diff is broadcast to all clients. Engine support exists in
  `Inis.Core/Debug/DebugCommandApi.cs`.
- **Server:** ASP.NET Core **.NET 10**, **Scalar** API docs, **PostgreSQL + EF
  Core**, **JWT access + refresh** auth, **friend requests**, **no** progression/
  leveling. One **`docker compose`**, API on **port 80** behind a reverse proxy at
  **inis.aricummings.com** (proxy terminates TLS). `INISServer/` is kept
  **extraction-ready** to later become its own repo/submodule.
- **Release CI/CD (Phase 12):** tag-driven GitHub Actions building `.msi`, `.dmg`,
  Flatpak, AppImage, `.ipa`, `.apk`, and the server image.

## 3. Status (phases)

| Phase | Scope | State |
|------|-------|-------|
| 0 | Scaffolding (solution, projects, CI, docs) | **DONE**, CI green |
| 1 | Assets + verified data | **DONE** (per-row `verified` flags; art via `tools/gen-art.mjs`) |
| 2 | Core rules engine | **DONE**, CI green (engine tests) |
| 4 | AI opponents + soak tests | **DONE**, CI green (run #9) |
| 5 | Server game sessions over WebSocket | **DONE**, CI green — lobbies, authoritative WS sessions, AI seats, redacted sync, EF migrations + game persistence, integration tests |
| 3 | Godot client (2.5D, offline/hotseat) | **DONE** — design system, menus, mode/setup, engine-driven HUD, 2.5D board (tilted 3D, textured hex tiles, low-poly pieces, orbit/pan/zoom, click-to-target). Headless-smoke-validated. |
| 6 | Online multiplayer in client | **DONE** — auth/lobby UI, WebSocket game sync, reconnection, spectator-ready; validated E2E vs a live server (Postgres + INISServer) |
| 7 | LAN multiplayer | **DONE** — client-hosted authoritative session (same WS protocol) + UDP discovery; loopback-validated |
| 8 | Settings, audio, polish, Debug screen | **DONE** — settings (persisted + live), original procedural audio (SFX + ambient), gated Debug/Cheat (works online via DebugCommand) |
| 9 | Cross-platform export & packaging | not started |
| 10 | *Seasons of Inis* expansion | later |
| 11 | 6–8 player extended mode | later |
| 12 | Release CI/CD (installers) | later |

**Recommended next order:** **Phase 9** (cross-platform export) → 10 → 11 → 12.
Phases 0–8 are done. Client work needs a session that can run the Godot
editor; Phase 5/AI/engine work only needs the .NET SDK + CI.

## 4. Repo layout & key files

```
Inis.slnx                      # solution (engine + tests + server)
Inis.Core/                     # SHARED rules engine (net8.0) — single source of rules
  Model/        GameState, PlayerState, TerritoryState, ClashState, DraftState,
                PendingDecision, Enums (GamePhase, PendingKind, MoveType, ...)
  Data/         GameData (loads embedded JSON), CardDefinition, TerritoryDefinition,
                cards.json, territories.json  (paraphrased text, `verified` flags)
  Rules/        GameEngine (THE engine: Apply/LegalMoves), GameSetup, VictoryEvaluator
  Effects/      EffectRegistry (one handler per card)
  Moves/        Move (intent), GameEvent
  Ai/           HeuristicAi, AiRunner
  Debug/        DebugCommandApi (cheat, server-authoritative)
Inis.Core.Tests/               # xUnit; CI runs these
INISServer/                    # ASP.NET Core .NET 10: Auth/Friends/Game endpoints,
                               # EF Core/Postgres, Scalar, Dockerfile, docker-compose
game/                          # Godot 4.4 .NET client; art under game/art/ (SVG)
tools/gen-art.mjs              # regenerates card/tile SVGs from the data
docs/                          # plan.md, rules.md, design.md, protocol.md, build.md, credits.md
.github/workflows/ci.yml       # build + test (.NET) + docker image build
```

The engine's public contract is `GameEngine.Create(...)`, `engine.Pending`,
`engine.LegalMoves()`, `engine.Apply(Move)`, `engine.LastEvents`. The client and
server both drive a game ONLY through these.

## 5. Environment notes (important)

- **Toolchain install (when egress is open):** .NET 10 SDK via `dotnet-install.sh`
  to `~/.dotnet`; also install the **.NET 8 runtime** (`--channel 8.0 --runtime
  dotnet`) so the net8 engine tests' host runs locally. For `dotnet ef`, install
  the tool and set `DOTNET_ROOT=$HOME/.dotnet`. **Godot 4.4 (.NET)**: download the
  `mono_linux_x86_64` build from the GitHub release; validate the client headless
  with `godot --headless res://scenes/SmokeTest.tscn` (CI does not build Godot) —
  rebuild the C# (`dotnet build game/INISOnline.csproj`) after editing client code
  before re-running, and `--headless --import` after adding scenes/assets.
- **Git/GitHub work** via a separate proxy regardless. Push only to the feature
  branch (`claude/confident-lovelace-fclngx`); the proxy restricts pushes to the
  current branch.

## 6. Conventions (follow these)

- **Branch:** `claude/confident-lovelace-fclngx`. Develop and push there. **Do NOT
  open a PR unless explicitly asked.** Use `git push -u origin <branch>` with
  exponential-backoff retries on network errors.
- **Commits** end with:
  ```
  Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HpZT6SEd7VtLu85nJmMeSX
  ```
- **Do NOT** put the model identifier / model name into commits, code, PRs, or any
  pushed artifact (chat only).
- **IP hygiene:** keep all card/rules text **paraphrased**; art **original**.
- **Data integrity:** card/tile JSON carries a `verified` flag; keep base-game
  composition (16 territories, 23 action, 16 advantage, 30 epic). Regenerate art
  with `node tools/gen-art.mjs` after data edits.
- **GitHub MCP** tools are scoped to `0xnullsect0r/inisonline`. CI status:
  `actions_list`/`actions_get` on `ci.yml`. Large MCP results are saved to a file;
  parse with `python3`/`jq`.

## 7. Verify

```bash
# (fresh session) install SDK if needed, then:
dotnet build Inis.slnx
dotnet test Inis.Core.Tests
dotnet run --project INISServer            # Scalar at /scalar
cd INISServer && docker compose up --build # Postgres + API on :80
# Client: open ./game in Godot 4.4 (.NET); run the Main scene.
```
CI (build + test + docker image) runs on every push; gate work on it being green.

## 8. Known follow-ups / tech debt

- **Server-reload determinism — FIXED in Phase 5.** `DeterministicRng` now tracks a
  draw `Cursor`; `GameState.RngCursor` persists it and the engine fast-forwards the
  seeded stream on construction. The draft leftover deck moved into `DraftState`.
  Model collections are `init`-settable so `GameState` round-trips through
  `Inis.Core/Net/InisJson`. Covered by `ReloadDeterminismTests`.
- **Redaction over-hides advantages (minor).** `PlayerView.Redact` masks an
  opponent's whole hand including face-up Advantage cards (engine stores them in
  `Hand`). Anti-cheat is safe (never leaks); reveal public advantages later.
- `Effects/EffectRegistry` and `Exploration` use `GameData.Default` rather than the
  engine's injected `Data` — fine for the default catalogue, fix when supporting
  alternate content sets (expansion modules).
- Several Triskel reactive-timing windows and some Advantage/Epic effects are
  modeled as legal no-ops; see `docs/rules.md` "documented simplifications". Flesh
  out as needed (notably for Phase 10).

## 9. Phase 5 — DONE (what was built)

Server is authoritative per `docs/protocol.md`. Key types:
- **`Inis.Core/Net`** (shared with the future client): `InisJson` (canonical
  (de)serialization), `Protocol`/`Envelope`/`ServerMessages`/`MoveCodec` (wire
  contract — the canonical intent is a `Move` echoed under type `"Intent"`; named
  verbs + `DebugCommand` also map), and **`PlayerView.Redact`** (per-player view:
  reveals the recipient's own hidden info, masks everyone else's hands/draft hands
  and the secret draw zones to counts using the `"?"` sentinel; clears IntentLog).
- **`INISServer/Game`**: `GameSessionManager` (singleton — in-memory lobbies +
  live `GameSession`s; rebuilds a missing session from the DB, resuming the engine
  deterministically). `GameSession` is the single writer (a `SemaphoreSlim` gate):
  maps intents→`Move`, applies authoritatively, auto-plays AI seats via
  `HeuristicAi` to the next human decision, persists, then broadcasts per-player
  redacted `StateSync` + `Event`s + a `TurnPrompt`. Reconnection replays a full
  StateSync; non-seated users connect as spectators; `DebugCommand` goes through
  `DebugCommandApi` and is broadcast (works online, audit-logged).
- **Lobbies** (`Endpoints/LobbyEndpoints`): `POST /lobbies` (capacity 2–5),
  `GET /lobbies`, `GET/POST /lobbies/{id}`, `/join` (open seat or `{code}`),
  `/leave`, `/ready`, `/seats/{i}/ai`, `/invite` (friends only), `/start` →
  `{ gameId }`; `GET /games/{id}` status.
- **Persistence**: `Game` entity (jsonb `StateJson` + `SeatsJson` on Postgres);
  `EnsureCreated()` replaced with EF migrations (`Data/Migrations/InitialCreate`)
  + `db.Database.Migrate()`; `DesignTimeDbContextFactory` keeps tooling off a live
  DB. Tests host the app over in-memory Sqlite (model `EnsureCreated`).
- **Auth fix**: disabled JWT inbound claim remapping (`MapInboundClaims=false`,
  `NameClaimType="unique_name"`) so endpoints can read `FindFirstValue("sub")`.

Integration tests: `INISServer.Tests` (WebApplicationFactory) — auth, friends, and
a scripted WebSocket bot playing a full AI-filled game to `GameOver`. Wired into CI
(`dotnet test INISServer.Tests`).

## 10. Phases 3 & 6 — DONE (client)

- **Phase 3 (offline/hotseat + 2.5D board):** code-built Celtic theme/design system
  (`game/src/Theme`), `ScreenManager` navigation, MainMenu/ModeSelect/GameSetup, and
  a `GameHud` driven by `IGameSource`. `LocalGame` runs the embedded engine (legal
  moves, AI auto-play). `game/src/Board` renders the 2.5D board into a SubViewport:
  tilted orbit/pan/zoom camera, flat textured hex tiles, low-poly clan/building
  meshes, gold highlight, raycast picking → click-to-target card play.
- **Phase 6 (online MP):** `IGameSource` abstracts the HUD source so `LocalGame`
  (offline) and `RemoteGame` (online) share one HUD. `game/src/Net`: `Session`
  (tokens+endpoint), `InisHttp` (REST), `RemoteGame` (`ClientWebSocket` reusing
  `Inis.Core/Net`; background receive loop, main-thread `Poll`, auto-reconnect).
  Screens: AuthScreen, OnlineMenu, OnlineLobby (poll + ready/AI-fill/start, auto-
  connects on start). **Validated E2E against a live server** by
  `scenes/OnlineSmoke.tscn` (register→lobby→start→full WS game to GameOver).

**Client validation recipe** (no CI for Godot): start Postgres + `dotnet run
--project INISServer` (set `ConnectionStrings__Postgres`, `ASPNETCORE_URLS`); then
`dotnet build game/INISOnline.csproj` and `godot --headless res://scenes/SmokeTest.tscn`
(offline) and `.../OnlineSmoke.tscn` (online, `INIS_SERVER` env overrides the URL).
Always export `DOTNET_ROOT=$HOME/.dotnet` for the Godot run.

### Phase 7 starter notes (LAN, next undone)
- Goal: the **client hosts** an embedded authoritative session and LAN peers join
  via the *same* WebSocket protocol — one netcode path. `RemoteGame` already speaks
  it, so a LAN client just points at `ws://<host-ip>:<port>/ws/game/{id}`.
- Host side: run an in-process session that owns a `GameEngine` and does the same
  redacted broadcast as `INISServer/Game/GameSession` (consider extracting the
  session/broadcast core so host + server share it). A lightweight WS server in the
  client (e.g. `HttpListener` WebSockets, or Godot's `TcpServer` + `WebSocketPeer`).
- Discovery: UDP broadcast a small beacon (game name, host ip/port); a LAN browser
  screen lists beacons and connects. No auth needed on LAN (or a simple room code).
