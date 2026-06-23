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

| Platform | Raw export | Release installer | Host / signing |
|----------|-----------|-------------------|----------------|
| Linux | binary (+ `data_*`) | `.flatpak` | any |
| Windows | `.exe` | `.msi` (WiX) | any |
| macOS | `.app`/`.dmg` | `.dmg` (signed) | macOS runner; codesign + notarization |
| Android | `.apk`/`.aab` | Play Store (manual) | Android SDK + keystore; `.NET mobile` workload |
| iOS | Xcode → `.ipa` | App Store (manual) | macOS + Xcode + Apple signing; `.NET mobile` workload |

Desktop installers are produced automatically on a release tag (see below).
Mobile builds are produced **manually** — see [`release-mobile.md`](release-mobile.md).
Mobile (.NET) export requires Godot 4.2+ and the `.NET mobile` workload
(`dotnet workload install android` / the iOS workload on macOS).

## Release automation (Phase 12)
`.github/workflows/ci.yml` is the per-push build/test gate.
`.github/workflows/release.yml` runs on `v*` tags and produces the **desktop
installers** plus the server image:

| Job | Runner | Output |
|-----|--------|--------|
| `linux-flatpak` | ubuntu | `INISOnline.flatpak` (via `flatpak-builder`) |
| `windows-msi` | windows | `INISOnline.msi` (via WiX v4/v5) |
| `macos-dmg` | macOS | `INISOnline.dmg` (signed + notarized) |
| `server-image` | ubuntu | `ghcr.io/<owner>/<repo>/inisserver:<tag>` + `latest` |
| `release` | ubuntu | GitHub Release with all artifacts attached |

To cut a release: `git tag v0.1.0 && git push origin v0.1.0`.

**Mobile (iOS / Android) is intentionally NOT in this pipeline.** App Store and
Play Store submissions require store metadata, screenshots, and review that can't
be meaningfully automated from a tag push. The full manual process —
including the build commands and how to upload to App Store Connect and the
Google Play Console — lives in [`release-mobile.md`](release-mobile.md).

### Packaging support files
- `packaging/linux/` — Flatpak manifest (`com.aricummings.INISOnline.yml`),
  `.desktop` entry, AppStream `.metainfo.xml`, and the launcher script.
- `packaging/windows/INISOnline.wxs` — WiX installer definition (harvests the
  whole export directory; adds a Start-menu shortcut and upgrade logic).

### macOS signing note
The DMG job signs with the certificate in `APPLE_CERT_P12` and notarizes via
`notarytool`. For a **directly-distributed** DMG (outside the Mac App Store) the
certificate must be a **"Developer ID Application"** certificate — not the "Apple
Distribution" certificate used for store uploads. Required secrets:

| Secret | Purpose |
|--------|---------|
| `APPLE_CERT_P12` | Base64-encoded `.p12` "Developer ID Application" certificate |
| `APPLE_CERT_PASSWORD` | Password for the `.p12` |
| `APPLE_TEAM_ID` | 10-character Apple Team ID |
| `APPLE_ID` | Apple ID email (notarization) |
| `APPLE_APP_PASSWORD` | App-specific password (notarization) |
