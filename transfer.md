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
| 3 | Godot client (2.5D, offline/hotseat) | **NOT STARTED** — needs Godot editor |
| 5 | Server game sessions over WebSocket | **NOT STARTED** — .NET, CI-verifiable |
| 6 | Online multiplayer in client | not started |
| 7 | LAN multiplayer | not started |
| 8 | Settings, audio, polish, Debug screen | not started |
| 9 | Cross-platform export & packaging | not started |
| 10 | *Seasons of Inis* expansion | later |
| 11 | 6–8 player extended mode | later |
| 12 | Release CI/CD (installers) | later |

**Recommended next order:** **Phase 5** (CI-verifiable .NET) → **Phase 3** (Godot)
→ 6 → 7 → 8 → 9 → 10 → 11 → 12. Phase 3 needs a session that can run the Godot
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

- **This session could NOT compile**: the original container booted before egress
  was opened, so `builds.dotnet.microsoft.com` (the .NET SDK installer host) is
  blocked and `dotnet`/`godot` are not preinstalled. Validation here relied on CI.
- **Network access is fixed at container boot.** A **fresh session** (egress now
  set to Full/Custom) can install the .NET 10 SDK and reach the rules sites.
- **Git/GitHub work** via a separate proxy regardless. Push only to the feature
  branch; the proxy restricts pushes to the current branch.

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

- **Server-reload determinism (do in Phase 5):** `GameEngine._rng` is re-seeded
  from `state.Seed` on construction and `_draftDeck` is an instance field — neither
  is persisted. Persisting/reconstructing a game mid-draft or mid-game will desync
  RNG. Add an RNG cursor/counter (and stash the draft leftover deck) into
  `GameState` so a reload reproduces subsequent draws exactly.
- `Effects/EffectRegistry` and `Exploration` use `GameData.Default` rather than the
  engine's injected `Data` — fine for the default catalogue, fix when supporting
  alternate content sets (expansion modules).
- Several Triskel reactive-timing windows and some Advantage/Epic effects are
  modeled as legal no-ops; see `docs/rules.md` "documented simplifications". Flesh
  out as needed (notably for Phase 10).

## 9. Phase 5 starter notes (next undone, CI-verifiable)

Goal: turn `INISServer/Endpoints/GameEndpoints.cs` (currently a WS echo stub) into
real authoritative game sessions, per `docs/protocol.md`:
- A `GameSessionManager` (singleton) holding `GameEngine` per game id; thread-safe.
- Lobbies: create/join (invite code + friend invite), choose 2–5 seats + AI fill,
  ready-up, start → builds the engine with `SeatConfig`s (+ `IsAi` seats).
- WS `/ws/game/{id}` (JWT via `?access_token`, already wired in `Program.cs`):
  parse intents → map to `Move` → `engine.Apply` → broadcast **per-player redacted**
  `StateSync`/`Diff` + `Event` + `TurnPrompt`; handle reconnection (replay
  StateSync) and spectators.
- For AI seats, after each human move drive `HeuristicAi`/`AiRunner` until the next
  human decision.
- `DebugCommand` → `DebugCommandApi.Apply` → broadcast synced diff (works online).
- Replace `EnsureCreated()` with EF migrations; persist active games (jsonb) for
  reload (apply the RNG-cursor fix above).
- Keep CI green; add integration tests (auth + friends + a scripted WS bot playing
  a full game).
