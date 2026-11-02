# Android release builds

How the Android app gets from a tag to Google Play (#118, #125).
`.github/workflows/android-release.yml` builds it against the production API and
uploads it to the internal testing track, for a tag such as `android-v1.2.0`, or when
started by hand. Once tested there, the same bundle is promoted to production.

## What a release build is

| | Debug | Release |
|---|---|---|
| API | `http://10.0.2.2:5068`, the API on the development machine | `-PapiBaseUrl`, the production API in the release workflow |
| Plain HTTP | Allowed, for that local API | **Refused**: HTTPS only (`usesCleartextTraffic=false`) |
| R8 | Off | Shrinks code and resources, and obfuscates; `mapping.txt` restores names |
| Signing | The debug key | The upload key, from the environment |
| `versionCode` / `versionName` | 1 / 1.0.0 | `-PversionCode` (the workflow's run number plus 100) / `-PversionName` (the tag without `android-v`) |
| Privacy policy and terms links | Hidden | `-PprivacyPolicyUrl`, `-PtermsUrl` |

The upload key comes from four environment variables: `ANDROID_KEYSTORE_PATH`,
`ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` and `ANDROID_KEY_PASSWORD`. Without
them the build is unsigned, which is fine for checking it locally, but Play refuses it.

**Play App Signing**: Google holds the key that signs what users install; the upload key
only proves an upload is ours. A lost or leaked upload key can be reset in Play Console,
so it's not a single point of failure.

## Setup, once

1. **Play Console:** create the developer account and the app (`com.golfprogress.app`), and
   keep Play App Signing on (the default).
2. **The upload key**, generated once and kept in a password manager:

   ```powershell
   keytool -genkeypair -keystore upload.jks -alias upload -keyalg RSA -keysize 4096 -validity 10000
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("upload.jks")) | Set-Clipboard
   ```

3. **The first upload by hand.** Play only accepts uploads through its API once the app
   has a first release. Build the bundle locally, signed with the upload key and against
   production, with `versionCode` 1 so the workflow's later uploads are higher:

   ```powershell
   cd clients/android
   $env:ANDROID_KEYSTORE_PATH = "C:\path\to\upload.jks"
   $env:ANDROID_KEY_ALIAS = "upload"
   $env:ANDROID_KEYSTORE_PASSWORD = Read-Host "Keystore password"
   $env:ANDROID_KEY_PASSWORD = Read-Host "Key password"
   ./gradlew :app:bundleRelease "-PversionCode=1" "-PversionName=1.0.0" `
     "-PapiBaseUrl=https://api.golfprogress.app" `
     "-PprivacyPolicyUrl=https://golfprogress.app/privacy" "-PtermsUrl=https://golfprogress.app/terms"
   ```

   Then upload `app/build/outputs/bundle/release/app-release.aab` to **Testing → Internal
   testing**.
4. **A service account for uploads:** in Google Cloud, create a service account with a JSON
   key. In Play Console → **Users and permissions**, invite its email with **Release to
   testing tracks** for this app only.
5. **In GitHub,** create the environment **`play`** and give it:
   - secret `ANDROID_UPLOAD_KEYSTORE_BASE64`: the key file, base64-encoded (step 2);
   - secrets `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` (`upload`) and
     `ANDROID_KEY_PASSWORD`;
   - secret `PLAY_SERVICE_ACCOUNT_JSON`: the service account's JSON key;
   - variable `PLAY_API_URL`: the production API, `https://api.golfprogress.app`. The build
     refuses anything but `https://`;
   - variables `PRIVACY_POLICY_URL` and `TERMS_URL` (repository level, shared with the web
     app): `https://golfprogress.app/privacy` and `https://golfprogress.app/terms`. The
     build refuses to run without them, since Play requires a privacy policy.
6. **Crash reporting** (#119): the repository-level `SENTRY_ANDROID_DSN`,
   `SENTRY_ANDROID_PROJECT`, `SENTRY_ORG` and `SENTRY_AUTH_TOKEN`, as in
   `docs/deployment.md` → "Crash reporting". Without them the build still works, and the
   app reports nothing. A bundle built by hand (step 3) reports nothing either, unless you
   add `-PsentryDsn=…`.

## Releasing

```powershell
git tag android-v1.0.0
git push origin android-v1.0.0
```

The workflow runs the Android tests and builds the signed bundle. It uploads the bundle to
internal testing together with R8's mapping file, so Play Console shows readable stack
traces, and keeps the mapping as a workflow artifact for 90 days. Testers on the internal
track get the update within minutes.

**To production:** once it works on the internal track, open the release in Play Console
and **Promote release → Production**, with a staged rollout (e.g. 20%, then 100% after a
few days without new crashes). The bundle is the same one, so it isn't rebuilt. Play
reviews the first production release, which can take a few days.

`versionCode` is this workflow's run number plus 100, which keeps it above the manual
first upload's 1. If Play ever has a higher one, e.g. from another upload by hand, the
upload is refused: run the workflow again until it passes, or upload once more by hand
with a higher `-PversionCode`.

## Checking a release build locally

R8 problems show up only when the minified app runs, so try one after changing dependencies
or the R8 rules (`app/proguard-rules.pro`). Pointed at the local API, with plain HTTP
allowed for this purpose only:

```powershell
cd clients/android
./gradlew :app:assembleRelease "-PapiBaseUrl=http://10.0.2.2:5068" -PallowCleartext=true
```

Sign the APK with any key (Android won't install it unsigned), install it on an emulator,
and register, log a round with holes, and open Progress and the round. Those screens use
every kind of API model, and a missing keep rule shows up as an error message or a crash
(`adb logcat`).

To sign with the upload key instead, set the four `ANDROID_KEY…` variables first.
`apksigner verify --print-certs app/build/outputs/apk/release/app-release.apk` shows which
key signed it.
