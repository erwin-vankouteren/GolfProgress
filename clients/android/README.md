# Android client

The GolfProgress Android app.

**Status:** the Gradle project and the API client module exist (#65). The app module is
created in #77 (app shell, theme and navigation).

## Stack

Kotlin. The app (#77) adds Jetpack Compose, Material 3 and Hilt, for Android 8.0
(API 26) and later. Tests with JUnit and Compose UI tests.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Getting started

Needs JDK 17. The Gradle wrapper downloads Gradle itself.

```bash
cd clients/android
./gradlew build          # generates the API client, compiles and tests
```

Or open the folder in Android Studio. Dependency versions are in
[`gradle/libs.versions.toml`](gradle/libs.versions.toml).

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
