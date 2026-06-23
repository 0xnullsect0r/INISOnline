# Mobile releases — iOS (App Store Connect) & Android (Google Play)

The release CI builds **desktop** installers only (Flatpak, MSI, DMG). Mobile
builds are produced **manually** because store submission requires metadata,
screenshots, age ratings, and human review that a tag push can't supply.

This guide is the end-to-end process: build a signed artifact, then upload it to
App Store Connect / the Google Play Console.

Both platforms use the bundle/application id **`com.aricummings.inisonline`**
(set in `game/export_presets.cfg`). Bump `version/name` + `version/code` (Android)
and `application/short_version` + `application/version` (iOS) before each release.

---

## Prerequisites (one-time)

### Tooling
- **Godot 4.4+ (.NET/Mono)** with the **Android** and **iOS** export templates
  installed (Editor → *Manage Export Templates*, or place them under the
  templates dir).
- **.NET 10 SDK** and the mobile workloads:
  ```bash
  dotnet workload install android      # Android
  dotnet workload install ios          # iOS (macOS only)
  ```
- **Android:** JDK 17, the Android SDK + command-line tools, and `keytool`
  (ships with the JDK).
- **iOS:** a **Mac** with **Xcode** (and `xcrun`, `altool`/`notarytool`,
  `xcodebuild`). iOS builds can only be produced on macOS.

### Build the C# assemblies first
Godot's .NET export needs the solution built in export config:
```bash
dotnet build game/INISOnline.sln -c ExportRelease
```

---

## Android → Google Play Console

### 1. Create a release keystore (one-time)
Keep this file and its passwords safe and backed up — **if you lose it you cannot
update the app** under the same listing.
```bash
keytool -genkeypair -v \
  -keystore inis-release.keystore \
  -alias inis \
  -keyalg RSA -keysize 2048 -validity 10000
```

### 2. Point Godot at the SDK + keystore
In the Godot editor: *Editor → Editor Settings → Export → Android*
- **Android SDK Path** → your SDK location
- **Debug Keystore** is only for local testing.

In *Project → Export → Android (preset) → Options → Keystore*:
- **Release** → path to `inis-release.keystore`
- **Release User** → `inis` (the alias)
- **Release Password** → the keystore/key password

> The export preset stores the keystore path but **not** the password in the repo.
> Set the password in the editor or pass it on the headless command line.

### 3. Export an Android App Bundle (`.aab`)
Google Play requires an **`.aab`**, not an `.apk`. In the editor enable
*Gradle Build* (already set in the preset) and export. Headless:
```bash
cd game
godot --headless --export-release "Android" ../build/android/INISOnline.aab
```
- If the path ends in `.aab` Godot produces an App Bundle; `.apk` produces an APK
  (useful for sideload testing only).
- Make sure `version/code` is **higher** than any previously uploaded build —
  Play rejects duplicate version codes.

### 4. Upload to the Play Console
1. Sign in to <https://play.google.com/console> and open (or create) the app.
2. First time only: complete **App content** (privacy policy URL, data safety
   form, content rating questionnaire, target audience, ads declaration).
3. **Release → Testing → Internal testing** (recommended first) → *Create new
   release*.
4. **Play App Signing**: accept it. Google re-signs with a managed key; your
   keystore becomes the *upload* key.
5. Upload `INISOnline.aab`, add release notes, **Save → Review → Roll out**.
6. Promote Internal → Closed → Open → **Production** once it passes review.

### Store listing assets you'll need
- App icon **512×512** PNG (32-bit, with alpha).
- Feature graphic **1024×500** PNG/JPG.
- At least **2** phone screenshots (and tablet screenshots if you target tablets).
- Short description (≤80 chars) and full description (≤4000 chars).
- Privacy policy URL.

---

## iOS → App Store Connect

iOS distribution always goes through the App Store (no sideloading). The flow is:
**Godot exports an Xcode project → Xcode/`xcodebuild` archives + signs → upload to
App Store Connect**.

### 1. Apple Developer portal (one-time)
At <https://developer.apple.com/account> → *Certificates, Identifiers & Profiles*:
1. **Identifier**: register an App ID for `com.aricummings.inisonline`.
2. **Certificate**: create an **Apple Distribution** certificate (for store
   uploads). Download and install it into your Mac's Keychain.
3. **Provisioning Profile**: create an **App Store** distribution profile for the
   App ID, using that certificate. Download and double-click to install.

Note your **Team ID** (10 chars, on the Membership page).

### 2. Create the app record in App Store Connect (one-time)
At <https://appstoreconnect.apple.com> → *My Apps → +* :
- Platform **iOS**, name **INIS Online**, primary language, bundle id
  `com.aricummings.inisonline`, an SKU (any unique string).

### 3. Export the Xcode project from Godot
```bash
cd game
godot --headless --export-release "iOS" ../build/ios/INISOnline.ipa
```
Godot generates a Xcode project (and, with full signing configured, an `.ipa`).
In the iOS export preset set:
- **Application → Bundle Identifier** = `com.aricummings.inisonline`
- **Application → Short Version / Version** (bump every upload)
- the **Team ID** and provisioning profile / signing identity fields.

### 4. Archive, sign, and upload
Open the generated project in Xcode (or use `xcodebuild`):
```bash
# from the generated Xcode project directory
xcodebuild -scheme INISOnline -configuration Release \
  -archivePath build/INISOnline.xcarchive archive

xcodebuild -exportArchive \
  -archivePath build/INISOnline.xcarchive \
  -exportOptionsPlist ExportOptions.plist \
  -exportPath build/ipa
```
`ExportOptions.plist` should set `method = app-store` and your `teamID`. Then
upload:
```bash
# App-specific password from https://appleid.apple.com → Sign-In and Security
xcrun altool --upload-app -f build/ipa/INISOnline.ipa -t ios \
  --apple-id "YOUR_APPLE_ID_EMAIL" \
  --password "APP_SPECIFIC_PASSWORD"
# (or use Xcode → Organizer → Distribute App → App Store Connect)
```

### 5. Submit for review in App Store Connect
1. Once the build finishes processing it appears under the app's **TestFlight** /
   **App Store** build picker.
2. Fill in **App Information**, **Pricing**, **App Privacy** (data collection),
   age rating, and the version's **What's New**.
3. Attach the processed build, add screenshots, **Add for Review → Submit**.

### Store listing assets you'll need
- App icon **1024×1024** PNG (no alpha, no rounded corners).
- Screenshots for the required device sizes (at minimum **6.7"** and **6.5"**
  iPhone; iPad screenshots if you support iPad).
- Description, keywords, support URL, privacy policy URL.

---

## Version bump checklist (every release)
1. `game/export_presets.cfg`:
   - Android: bump `version/code` (integer, must increase) **and** `version/name`.
   - iOS: bump `application/version` **and** `application/short_version`.
2. `game/project.godot`: bump `config/version`.
3. Rebuild C# (`dotnet build game/INISOnline.sln -c ExportRelease`), export, upload.
4. Tag the desktop release too: `git tag vX.Y.Z && git push origin vX.Y.Z`.

## Secrets reference (if you later automate mobile in CI)
These are **not** used by the current `release.yml` (mobile is manual), but if you
add a self-hosted/macOS CI lane later, the signing material maps to:

| Secret | Purpose |
|--------|---------|
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 inis-release.keystore` |
| `ANDROID_KEYSTORE_PASSWORD` / `ANDROID_KEY_ALIAS` / `ANDROID_KEY_PASSWORD` | keystore creds |
| `APPLE_CERT_P12` / `APPLE_CERT_PASSWORD` | Apple Distribution cert, base64-encoded |
| `APPLE_PROVISIONING_PROFILE` | App Store profile, base64-encoded |
| `APPLE_TEAM_ID` | 10-char Team ID |
| `APPLE_ID` / `APPLE_APP_PASSWORD` | upload via `altool`/`notarytool` |
