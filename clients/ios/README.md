# iOS client

The GolfProgress iOS app.

**Status:** the app shell exists (#76): tabs, theme and the minimum-version check, on top
of the API client package (#64). Sign-in arrives in #70, and the features in M2.

## Stack

Swift and SwiftUI, for iOS 17 and later. Tests with XCTest / Swift Testing and XCUITest.

Building needs a Mac with Xcode. CI builds on GitHub's macOS runners, but only for
changes that affect the iOS client (`.github/workflows/ios.yml`), because those runners
cost 10x Linux minutes.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## The app

The Xcode project is **generated** from [`project.yml`](project.yml) with
[XcodeGen](https://github.com/yonaskolb/XcodeGen) and not committed, so there's no
hand-merged `.pbxproj`. On a Mac:

```bash
brew install xcodegen
cd clients/ios
xcodegen            # creates GolfProgress.xcodeproj; rerun after changing project.yml
open GolfProgress.xcodeproj
```

| Path | What |
|---|---|
| `GolfProgress/App/` | `GolfProgressApp`, `RootView` (the version gate) and `AppEnvironment`: the API client and app version, injected with `.environment` |
| `GolfProgress/Navigation/` | `MainTabView`: Home, Rounds, **Log round** (opens as a sheet), Practice and Progress. Profile opens from the avatar in each screen's top bar. `TabRouter` holds the selection, so it can be tested |
| `GolfProgress/Sections/` | Each section's screen, with the empty states from the wireframes until the features land |
| `GolfProgress/Version/` | `VersionCheck`: "Update required" when this build is older than `GET /client-config`'s minimum. It never blocks start-up, and if the check fails the app carries on |
| `GolfProgress/DesignSystem/DesignTokens.swift` | **Generated** from `design/tokens.json`; don't edit |
| `GolfProgress/Resources/Localizable.xcstrings` | String Catalog: all user-facing text |
| `GolfProgressTests/` | Swift Testing tests |

**Conventions:**
- **Styling:** colours, fonts and spacing only through `DesignTokens`, never literal values.
- **Text:** through `LocalizedStringKey`, so it goes into the String Catalog.
- **Controls:** at least `DesignTokens.touchTarget` (44 pt).
- **Lint:** SwiftLint runs with `--strict` (`.swiftlint.yml`).

**API URL:** `GOLFPROGRESS_API_BASE_URL` in `project.yml` (`http://localhost:5068` for
development; App Transport Security allows only local networks over plain HTTP).

**Linking:** the app links `OpenAPIRuntime` and `OpenAPIURLSession` directly, not only
through the API package. The package's generic code is compiled into the app, and without
those libraries the link step fails.

**CI** (`.github/workflows/ios.yml`) generates the project, lints, and runs the tests on
an available iPhone simulator, then prints how many tests ran.

## API client: `Packages/GolfProgressAPI`

A local Swift package. Apple's `swift-openapi-generator` build plugin generates the API's
types and a `Client` from `Sources/GolfProgressAPI/openapi.json` on every build. That file
is a copy of [`api/openapi.json`](../../api/): SwiftPM plugins can only read inside their
package. `api/generate-openapi.ps1` keeps the copy in sync, and CI fails if it isn't.

```swift
import GolfProgressAPI

let api = GolfProgressAPI.makeClient(serverURL: apiURL, tokens: session)

do {
    let me = try await api.getMe().ok.body.json
} catch {
    if let problem = APIProblem.from(error) {
        // problem.status, problem.detail, problem.messages(for: "playedAt")
    }
}
```

What `makeClient` adds around the generated client:

- **`BearerAuthMiddleware`** sends the access token on authenticated operations. On a
  `401` it asks the `AccessTokenProvider` to refresh once and retries. Sign-in operations
  never carry a token or trigger a refresh, so a wrong password stays a plain `401`. The
  session layer that implements `AccessTokenProvider` (Keychain storage, one shared
  refresh) is #70.
- **`ProblemDetailsMiddleware`** turns every `application/problem+json` error into a thrown
  `APIProblem`, with `messages(for:)` for per-field validation errors. A call therefore
  returns its success value or throws; views don't switch over each operation's error cases.
- **Open enums** (#134): `sessionType`, micro-win `category` and round `source` are plain
  strings in responses, because the API may add values at any time. Read them through
  `knownSessionType`, `knownCategory` and `knownSource` (`KnownValues.swift`), which give
  `.unknown` for a value this build doesn't know.
- **`DotNetDateTranscoder`** reads the API's timestamps, which have up to seven
  fractional-second digits that the default ISO 8601 decoder rejects.

Build and test on a Mac:

```bash
cd clients/ios/Packages/GolfProgressAPI
swift test
```

Or open `Package.swift` in Xcode. The app adds the package as a local dependency in #76.
