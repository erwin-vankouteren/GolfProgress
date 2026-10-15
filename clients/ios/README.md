# iOS client

The GolfProgress iOS app.

**Status:** not started. The Xcode project is created in #76 (app shell, theme and navigation).

## Stack

Swift 6 and SwiftUI, for iOS 17 and later. The API client is generated from
[`api/openapi.json`](../../api/) by Apple's `swift-openapi-generator`, in a local Swift
package (#64). Tests with Swift Testing and XCUITest.

Building needs a Mac with Xcode. CI builds on GitHub's macOS runners, but only for
changes that affect the iOS app, because those runners cost 10x Linux minutes (#59).

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Build and run

To be added with the project in #76: open the project, select a simulator, run and test.
