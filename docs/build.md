# Building & exporting

## Prerequisites
- **.NET 10 SDK** (engine, server, tests).
- **Godot 4.4+ (.NET/Mono build)** for the client.
- **Docker** (server deployment).

## Engine + server
```bash
dotnet build Inis.slnx
dotnet test Inis.Core.Tests
dotnet run --project INISServer            # local API + Scalar at /scalar
cd INISServer && docker compose up --build # Postgres + API on :80
```

## Client (Godot)
Open `game/` in Godot 4.4 (.NET). First build restores the `Inis.Core` reference.
Run the `Main` scene. Configure the server endpoint per build (export feature tag
or a settings field).

## Cross-platform export (Phase 9)
Export presets live in `game/export_presets.cfg` (per platform). Targets:

| Platform | Output | Notes |
|----------|--------|-------|
| Windows | `.exe` | + later `.msi` (Phase 12) |
| macOS | `.app` | + later `.dmg`, notarization |
| Linux | binary | + later Flatpak / AppImage |
| Android | `.apk`/`.aab` | .NET mobile export; keystore signing |
| iOS | Xcode project → `.ipa` | .NET mobile export; Apple signing |

Mobile (.NET) export requires Godot 4.2+; ensure the iOS/Android export templates
and the .NET mobile workload are installed.

## Release automation (Phase 12)
See `.github/workflows/` — CI builds/tests on every push; a tagged release builds
and publishes installers (`.dmg`, `.msi`, Flatpak, AppImage, `.ipa`, `.apk`) and the
server image. Signing secrets are provided via repository/Action secrets.
