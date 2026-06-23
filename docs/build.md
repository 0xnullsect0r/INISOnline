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
Open `game/` in Godot 4.4 (.NET). First build restores the `Inis.Core` reference and
generates `game/INISOnline.sln` (committed). Run the `Main` scene.

**Server endpoint per build.** The client reads `application/config/server_url` from
the project settings (`Session.InitFromProject`), so each build can target a different
backend. Override it for an export, e.g.:
`godot --headless --export-release "Linux" out --custom-features ...` then edit the
setting, or maintain per-target `project.godot` overrides. Default:
`https://inis.aricummings.com`.

## Cross-platform export (Phase 9)
Export presets live in `game/export_presets.cfg` (one per platform). Install the
Godot **export templates** (`4.4-stable` mono) and, for the matching host, the
platform SDKs, then export from the editor or headless:

```bash
# .NET export REQUIRES the solution file game/INISOnline.sln (committed). Then:
cd game
godot --headless --export-release "Linux"   ../build/linux/INISOnline.x86_64
godot --headless --export-release "Windows Desktop" ../build/windows/INISOnline.exe
godot --headless --export-release "macOS"    ../build/macos/INISOnline.dmg   # on macOS
godot --headless --export-release "Android"  ../build/android/INISOnline.apk # Android SDK + keystore
godot --headless --export-release "iOS"      ../build/ios/INISOnline.ipa     # on macOS + Xcode
```

The .NET export emits the executable plus a `data_INISOnline_*` folder with the
managed assemblies. **Validated:** the headless Linux export produces a standalone
binary that boots and links the engine (`INIS engine linked. Cards: 69 …`).

| Platform | Output | Host / signing |
|----------|--------|----------------|
| Linux | binary (+ `data_*`) | any; + later Flatpak / AppImage |
| Windows | `.exe` | any; codesign + later `.msi` (Phase 12) |
| macOS | `.dmg`/`.app` | macOS runner; codesign + notarization |
| Android | `.apk`/`.aab` | Android SDK + keystore; `.NET mobile` workload |
| iOS | `.ipa` (via Xcode) | macOS runner + Apple signing; `.NET mobile` workload |

Mobile (.NET) export requires Godot 4.2+ and the `.NET mobile` workload
(`dotnet workload install android` / the iOS workload on macOS).

## Release automation (Phase 12)
`.github/workflows/ci.yml` is the per-push build/test gate. `.github/workflows/release.yml`
runs on `v*` tags and:
- builds the **desktop** exports (Linux, Windows) via `chickensoft-games/setup-godot`
  (it installs Godot .NET + export templates), zips them, and uploads them;
- builds **macOS/iOS** (on a macOS runner) and **Android** only when the repo
  variables `ENABLE_APPLE_BUILDS` / `ENABLE_ANDROID_BUILDS` are `true` and the
  signing secrets (Apple cert/team, Android keystore) are configured;
- builds and pushes the **`INISServer`** image to **GHCR**
  (`ghcr.io/<owner>/<repo>/inisserver:<tag>` + `latest`);
- creates a **GitHub Release** and attaches all artifacts.

### Required secrets for mobile/macOS signing

| Secret | Purpose |
|--------|---------|
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded release `.keystore` |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias inside the keystore |
| `ANDROID_KEY_PASSWORD` | Key password |
| `APPLE_CERT_P12` | Base64-encoded `.p12` distribution certificate |
| `APPLE_CERT_PASSWORD` | Password for the `.p12` |
| `APPLE_TEAM_ID` | 10-character Apple Team ID |
| `APPLE_PROVISIONING_PROFILE` | Base64-encoded `.mobileprovision` |
| `APPLE_ID` | Apple ID email (notarization, optional) |
| `APPLE_APP_PASSWORD` | App-specific password (notarization, optional) |

Set repo **variables** `ENABLE_APPLE_BUILDS`, `ENABLE_ANDROID_BUILDS`, and
optionally `ENABLE_NOTARIZATION` to `true` to enable those jobs.

To cut a release: `git tag v0.1.0 && git push origin v0.1.0`.
