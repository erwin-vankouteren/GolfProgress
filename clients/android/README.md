# Android client

The GolfProgress Android app.

**Status:** not started. The Gradle project is created in #77 (app shell, theme and navigation).

## Stack

Kotlin, Jetpack Compose and Material 3, for Android 8.0 (API 26) and later. Hilt for
dependency injection, and an API client generated from [`api/openapi.json`](../../api/)
by OpenAPI Generator (Retrofit + kotlinx.serialization) in an `:api-client` module
(#65). Tests with JUnit and Compose UI tests.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Build and run

To be added with the project in #77: open in Android Studio or use the Gradle wrapper,
run on an emulator, and test.
