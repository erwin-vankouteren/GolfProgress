# Android release builds

How the Android app gets from a tag to Google Play's internal testing track (#118).
`.github/workflows/android-release.yml` does it for a tag such as `android-v1.2.0`, or
when started by hand.

## What a release build is

| | Debug | Release |
|---|---|---|
| API | `http://10.0.2.2:5068`, the API on the development machine | `-PapiBaseUrl`, the staging API for now |
| Plain HTTP | Allowed, for that local API | **Refused**: HTTPS only (`usesCleartextTraffic=false`) |
| R8 | Off | Shrinks code and resources, and obfuscates; `mapping.txt` restores names |
| Signing | The debug key | The upload key, from the environment |
| `versionCode` / `versionName` | 1 / 1.0.0 | `-PversionCode` (the workflow's run number) / `-PversionName` (the tag without `android-v`) |
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
   has a first release: build the bundle locally (below, with the upload key and
   `-PapiBaseUrl`) and upload `app-release.aab` to **Testing → Internal testing**.
4. **A service account for uploads:** in Google Cloud, create a service account with a JSON
   key. In Play Console → **Users and permissions**, invite its email with **Release to
   testing tracks** for this app only.
5. **In GitHub,** create the environment **`play-internal`** and give it:
   - secret `ANDROID_UPLOAD_KEYSTORE_BASE64`: the key file, base64-encoded (step 2);
   - secrets `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` (`upload`) and
     `ANDROID_KEY_PASSWORD`;
   - secret `PLAY_SERVICE_ACCOUNT_JSON`: the service account's JSON key;
   - variable `STAGING_API_URL`: the staging API, `https://…`. The build refuses anything
     else;
   - variables `PRIVACY_POLICY_URL` and `TERMS_URL` once they're published (#121).

## Releasing

```powershell
git tag android-v1.0.0
git push origin android-v1.0.0
```

The workflow runs the Android tests and builds the signed bundle. It uploads the bundle to
internal testing together with R8's mapping file, so Play Console shows readable stack
traces, and keeps the mapping as a workflow artifact for 90 days. Testers on the internal
track get the update within minutes.

`versionCode` is this workflow's run number. If Play already has a higher one, e.g. from
the manual first upload, the upload is refused: run the workflow again until it passes, or
upload once more by hand with a higher `-PversionCode`.

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
