# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

INIS Online is a digital adaptation of the INIS board game (Celtic-mythology card-drafting / area-control) built with:
- **Client:** Godot 4.4 (.NET/C#) — 2.5D board with 3D pieces, all UI built in code (no .tscn UI scenes)
- **Server:** ASP.NET Core (.NET 10) with PostgreSQL + JWT auth
- **Shared engine:** `Inis.Core` (.NET 8.0) — the single authoritative rules implementation, used by both client and server

Fan project using original artwork and paraphrased rules (never Matagot's verbatim text or art).

## Build & Test Commands

```bash
# Build everything (engine + server + tests)
dotnet build Inis.slnx

# Run tests
dotnet test Inis.Core.Tests        # Rules engine unit tests (xUnit)
dotnet test INISServer.Tests       # Server integration tests (xUnit, in-memory SQLite)

# Run a single test
dotnet test Inis.Core.Tests --filter "FullyQualifiedName~TestMethodName"

# Note: Inis.Core targets net8.0. If only the .NET 10 runtime is installed,
# run engine tests with roll-forward: DOTNET_ROLL_FORWARD=Major dotnet test Inis.Core.Tests

# Run server locally
dotnet run --project INISServer

# Run server via Docker (Postgres + API on port 80)
cd INISServer && docker compose up --build

# Client: open game/ in Godot 4.4 (.NET), main scene is res://scenes/Main.tscn
```

## Architecture

### Solution Structure (`Inis.slnx`)

```
Inis.Core/           # Shared rules engine (net8.0, no UI/IO)
Inis.Core.Tests/     # xUnit tests for rules
INISServer/          # ASP.NET Core .NET 10 server
INISServer.Tests/    # Server integration tests
game/                # Godot 4.4 .NET client
```

### Rules Engine (`Inis.Core/`)

The engine is a deterministic state machine. Key public API:
- `GameEngine.Create(gameId, seed, seats, data?, options?)` — creates a fully set-up game
- `engine.Pending` — what decision the engine is waiting for (kind + player)
- `engine.LegalMoves()` — all valid moves for the pending player
- `engine.Apply(Move)` — apply an intent, mutate state
- `engine.LastEvents` — animatable facts from the last move

Key subsystems:
- **Model/** — `GameState`, `PlayerState`, `TerritoryState`, `PendingDecision`, `ReactionState`
- **Rules/** — `GameEngine`, `GameEngine.Reactions` (Triskel reaction windows: a
  `ReactionFrame` stack with persisted continuations; windows only open when an
  eligible holder exists and passing is always legal), `GameSetup`, `VictoryEvaluator`
- **Effects/** — `EffectRegistry` (one handler per card effect)
- **Data/** — `GameData` loads embedded `cards.json` / `territories.json`
- **Ai/** — `HeuristicAi` (deterministic heuristic AI for seat-filling)
- **Net/** — `Protocol.cs` (message envelope v1), `PlayerView.Redact()` (per-player state redaction)
- **Debug/** — `DebugCommandApi` (server-authoritative cheat commands)

Determinism: seeded `DeterministicRng` with resumable cursor — enables replay and server recovery.

### Server (`INISServer/`)

- **Endpoints/** — REST: Auth (login/register/refresh), Lobbies, Friends; WebSocket: `/ws/game/{gameId}`
- **Game/** — `GameSession` (per-game authoritative engine, single-writer semaphore, AI auto-play, redacted sync), `GameSessionManager` (singleton)
- **Data/** — EF Core + Postgres (Users, Games, Friendships, RefreshTokens)
- **Auth/** — JWT access+refresh tokens; WebSocket auth via `?access_token` query param

### Client (`game/src/`)

All UI is code-built (no pre-designed .tscn UI scenes). Single autoload: `AudioManager`.

- **Screens/** — Full-screen views managed by `ScreenManager` (cross-fade navigation)
- **Board/** — `BoardView` (SubViewport with 3D hex tiles + low-poly pieces, orbit/pan/zoom camera)
- **Game/** — `LocalGame` (offline/hotseat via embedded engine), `IGameSource` interface
- **Net/** — `RemoteGame` (online WebSocket), `WsGameSourceBase` (shared WS base class)
- **Lan/** — `LanHost` (embedded authoritative server), `LanDiscovery` (UDP broadcast), `LanClientGame`/`LanHostGame`

**IGameSource** is the key abstraction — `GameHud` talks only to this interface, enabling the same HUD across offline, LAN, and online modes. Implementations: `LocalGame`, `LanHostGame`, `LanClientGame`, `RemoteGame`.

### Networking

One WebSocket JSON protocol (v1) serves both online and LAN. The host is always authoritative: clients send intents, host validates with `Inis.Core`, broadcasts per-player redacted state. Opponent hands are never sent to clients (anti-cheat boundary).

### Game Modes

- **Offline/Hotseat** — embedded engine, no network
- **Online** — server-authoritative via INISServer
- **LAN** — one client hosts with embedded engine, others join via WebSocket + UDP discovery
- **AI** — `HeuristicAi` fills empty seats in all modes

### Game Options

- **Base** — 2–4 players, 17 action cards
- **Seasons of Inis** — 2–5 players (5th clan), the 4 new action cards + updated
  Exploration/Druid, season wheel (Sacred Festivals + seasonal modifiers),
  harbours & sea travel (`GameEngine.AreConnected`), island territories
- **Extended** — 2–8 players, doubled action deck (house-ruled)

## CI/CD

- **ci.yml** — every push: build solution (Release), run both test suites, build Docker image
- **release.yml** — on `v*` tags: export desktop binaries (Linux Flatpak, Windows MSI, macOS DMG), push server image to GHCR, create GitHub Release

## Key Documentation

- `docs/rules.md` — Rules transcription and per-card verification status
- `docs/protocol.md` — Network protocol specification
- `docs/design.md` — UI/UX design, 2.5D rendering approach
- `docs/build.md` — Cross-platform export and build details
- `transfer.md` — Session handoff guide (project status snapshot)
