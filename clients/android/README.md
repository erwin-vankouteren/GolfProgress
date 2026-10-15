# Android client

The GolfProgress Android app.

**Status:** the app shell exists (#77): navigation, theme and the minimum-version check,
on top of the API client module (#65). Sign-in arrives in #71, and the features in M2.

## Stack

Kotlin. The app (#77) adds Jetpack Compose, Material 3 and Hilt, for Android 8.0
(API 26) and later. Tests with JUnit and Compose UI tests.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Getting started

Needs JDK 17. The Gradle wrapper downloads Gradle itself.

```bash
cd clients/android
./gradlew :api-client:build   # the API client: generates, compiles and tests (no SDK needed)
```

Or open the folder in Android Studio. Dependency versions are in
[`gradle/libs.versions.toml`](gradle/libs.versions.toml).

## The app: `:app`

Jetpack Compose with Material 3 and Hilt, for Android 8.0 (API 26) and later, compiled
against API 37, which current AndroidX libraries require. AGP 9 compiles Kotlin itself, so
the module applies the Compose compiler plugin but not `kotlin-android`.

| Path | What |
|---|---|
| `MainActivity.kt` | Entry point; `Root` shows **Update required** or the main screen |
| `navigation/MainScreen.kt` | Bottom bar with Home, Rounds, **Log round** (centre), Practice and Progress; Profile from the avatar in the top bar, as in the wireframes |
| `sections/` | Each section's empty state from the wireframes, until the features land |
| `version/` | `VersionViewModel`: "Update required" when this build is older than `GET /client-config`'s minimum. It never blocks start-up, and if the check fails the app carries on |
| `di/AppModule.kt` | Hilt: the API client, the app version, the minimum-version source |
| `ui/theme/Theme.kt` | The Material 3 theme, built only from `DesignTokens`; light or dark follows the system |
| `ui/theme/DesignTokens.kt` | **Generated** from `design/tokens.json`; don't edit |
| `res/values/strings.xml` | All user-facing text |

**Conventions:**
- **Styling:** colours, type and spacing only through the theme and `DesignTokens`.
- **Text:** only through `strings.xml`.
- **Controls:** at least `DesignTokens.touchTarget` (48 dp).
- **Lint:** ktlint (`.editorconfig` lets `@Composable` functions be PascalCase).

**API URL:** `BuildConfig.API_BASE_URL`, which is `http://10.0.2.2:5068` in debug builds:
the development machine, as seen from the emulator.

**Tests** run on the JVM with Robolectric, including Compose UI tests, so no emulator is
needed: `./gradlew :app:testDebugUnitTest`.

```bash
./gradlew :app:assembleDebug        # needs the Android SDK (Android Studio installs it)
./gradlew :app:ktlintCheck :app:lintDebug :app:testDebugUnitTest
```

## API client: `:api-client`

A plain Kotlin/JVM module, so it builds and tests without the Android SDK; the app
module depends on it. On every build, OpenAPI Generator turns
[`api/openapi.json`](../../api/) into Retrofit interfaces with coroutines and
kotlinx.serialization models (`com.golfprogress.api.generated`, under `build/`, never
committed).

```kotlin
val api = GolfProgressApi(baseUrl, tokens = session)

val me = api.service<UsersApi>().getMe().unwrap()                 // typed UserResponse
val page = api.service<RoundsApi>().listMyRounds(skip = 0, take = 20)
val rounds = page.unwrap()
val total = page.totalCount()                                     // from X-Total-Count

try {
    api.service<RoundsApi>().createRound(request).unwrap()
} catch (problem: ApiProblem) {
    problem.messagesFor("playedAt")
}
```

- **Access token:** `AuthInterceptor` sends it on authenticated requests.
  `TokenAuthenticator` refreshes it once on a `401` and retries. Concurrent `401`s share
  one refresh, because each refresh rotates the refresh token. Sign-in requests never
  carry a token or trigger a refresh. The `AccessTokenProvider` (refresh token encrypted
  with an Android Keystore key) is #71.
- **Errors:** `unwrap()` returns the body or throws an `ApiProblem` (status, detail,
  `traceId`, and `messagesFor(field)` for validation errors).
- **Dates and ids:** the generated models use `java.time.OffsetDateTime` and
  `java.util.UUID`, read through the generator's own `Serializer`, which
  `GolfProgressApi` uses. `OffsetDateTime` keeps the seven fractional-second digits .NET
  writes.
- **Open enums** (#134): `sessionType`, micro-win `category` and round `source` are plain
  strings in responses, because the API may add values at any time. Read them through
  `knownSessionType`, `knownCategory` and `knownSource` (`KnownValues.kt`), which give
  `Unknown` for a value this build doesn't know.
