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
| Repo scaffolding | 0 | in progress |
| Game assets (SVG) | 1 | art pipeline + full piece/card/tile set done; card text data partial |
| Rules engine (`Inis.Core`) | 2 | scaffolding |
| Godot client (offline/hotseat) | 3 | scaffolding |
| AI opponents | 4 | heuristic AI + AI-vs-AI soak tests (CI green) |
| `INISServer` (ASP.NET, .NET 10) | 5 | scaffolding |
| Online multiplayer (client) | 6 | not started |
| LAN multiplayer | 7 | not started |
| Settings / audio / Debug screen | 8 | not started |
| Cross-platform export | 9 | not started |
| *Seasons of Inis* expansion | 10 | later |
| 6–8 player extended mode | 11 | later |
| Release CI/CD (installers) | 12 | later |

## Repository layout

```
INISOnline/
  Inis.Core/         # .NET class library — the shared rules engine (no UI / no I/O)
  Inis.Core.Tests/   # xUnit tests for the engine
  game/              # Godot 4.4 .NET client (references Inis.Core)
  INISServer/        # ASP.NET Core .NET 10 server + docker compose (references Inis.Core)
  assets/            # Source SVG art + export pipeline -> game/ import dirs
  docs/              # Rules transcription, protocol spec, design & build docs
  Inis.sln           # Solution: Inis.Core, Inis.Core.Tests, INISServer
```

The engine (`Inis.Core`) is the single source of game rules and is shared by both
the client (for offline / hotseat / LAN host) and the server (authoritative online
play), so logic is never duplicated.

## Building

Requires the **.NET 10 SDK** and **Godot 4.4+ (.NET/Mono build)**.

```bash
# Engine + server + tests
dotnet build Inis.sln
dotnet test Inis.Core.Tests

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
