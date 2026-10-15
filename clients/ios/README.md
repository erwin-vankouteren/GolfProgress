# iOS client

The GolfProgress iOS app.

**Status:** the API client package exists (#64). The Xcode project for the app is
created in #76 (app shell, theme and navigation).

## Stack

Swift and SwiftUI, for iOS 17 and later. Tests with XCTest / Swift Testing and XCUITest.

Building needs a Mac with Xcode. CI builds on GitHub's macOS runners, but only for
changes that affect the iOS client (`.github/workflows/ios.yml`), because those runners
cost 10x Linux minutes.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

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
