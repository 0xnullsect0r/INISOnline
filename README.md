# INIS Online

A cross-platform digital adaptation of **INIS** (Christian Martinez, Matagot, 2016)
— a Celtic-mythology card-drafting / area-control board game — built in **Godot 4
(.NET/C#)** with a centralized **ASP.NET Core (.NET 10)** multiplayer server.

> This is a fan project. It uses **original artwork** (not Matagot's), and game
> text is re-implemented for interoperability. INIS is © Matagot / Christian
> Martinez. This project is not affiliated with or endorsed by Matagot.

## Status

Early development. See the implementation plan and per-area docs in [`docs/`](docs/).

| Area | Phase | State |
|------|-------|-------|
| Repo scaffolding | 0 | **done** (CI green) |
| Game assets (SVG) | 1 | **done** — verified data + art pipeline |
| Rules engine (`Inis.Core`) | 2 | **done** (CI green) |
| Godot client (offline/hotseat) | 3 | **done** — theme, menus, setup, engine-driven HUD + 2.5D board (validated in the Godot editor) |
| AI opponents | 4 | **done** — heuristic AI + AI-vs-AI soak tests (CI green) |
| `INISServer` (ASP.NET, .NET 10) | 5 | **done** — lobbies, authoritative WebSocket sessions, AI seats, redacted sync, EF migrations + persistence (CI green) |
| Online multiplayer (client) | 6 | **done** — auth/lobby UI + WebSocket game sync (validated E2E against a live server) |
| LAN multiplayer | 7 | **done** — client-hosted session + UDP discovery (loopback-validated) |
| Settings / audio / Debug screen | 8 | **done** — settings, original procedural audio, gated Debug/Cheat |
| Cross-platform export | 9 | **done** — export presets for 5 targets; Linux export boots (validated) |
| *Seasons of Inis* expansion | 10 | **done** (toggle) — expansion cards + 5th clan/seat; season board/harbours/sea-travel later |
| 6–8 player extended mode | 11 | **done** — house-ruled scaling (8 clans, doubled deck) |
| Release CI/CD (installers) | 12 | **done** — tag-driven `release.yml` (exports + server image → GHCR + GitHub Release) |

## Repository layout

```
INISOnline/
  Inis.Core/         # .NET class library — the shared rules engine (no UI / no I/O)
  Inis.Core.Tests/   # xUnit tests for the engine
  game/              # Godot 4.4 .NET client (references Inis.Core)
  INISServer/        # ASP.NET Core .NET 10 server + docker compose (references Inis.Core)
  assets/            # Source SVG art + export pipeline -> game/ import dirs
  docs/              # Rules transcription, protocol spec, design & build docs
  Inis.slnx          # Solution: Inis.Core(+Tests), INISServer(+Tests)
```

The engine (`Inis.Core`) is the single source of game rules and is shared by both
the client (for offline / hotseat / LAN host) and the server (authoritative online
play), so logic is never duplicated.

## Building

Requires the **.NET 10 SDK** and **Godot 4.4+ (.NET/Mono build)**.

```bash
# Engine + server + tests
dotnet build Inis.slnx
dotnet test Inis.Core.Tests      # rules engine
dotnet test INISServer.Tests     # auth, friends + WebSocket game session

# Server locally
dotnet run --project INISServer

# Server via Docker (Postgres + API on port 80)
cd INISServer && docker compose up --build

# Client: open ./game in Godot 4.4 (.NET) and run, or export per docs/build.md
```

See [`docs/build.md`](docs/build.md) for cross-platform export details.

## License

Code is released under the repository [LICENSE](LICENSE). Third-party audio/art
attributions are tracked in [`docs/credits.md`](docs/credits.md).
