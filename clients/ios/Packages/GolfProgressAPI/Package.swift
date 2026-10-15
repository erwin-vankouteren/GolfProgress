// swift-tools-version: 5.9
//
// The GolfProgress API client for the iOS app (#64). Types and the client are generated
// at build time from Sources/GolfProgressAPI/openapi.json, a copy of api/openapi.json
// kept in sync by api/generate-openapi.ps1 and checked by CI.

import PackageDescription

let package = Package(
    name: "GolfProgressAPI",
    platforms: [
        .iOS(.v17),
        // So the package builds and tests with `swift test` on a Mac, without a simulator.
        .macOS(.v14),
    ],
    products: [
        .library(name: "GolfProgressAPI", targets: ["GolfProgressAPI"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-openapi-generator", from: "1.3.0"),
        .package(url: "https://github.com/apple/swift-openapi-runtime", from: "1.5.0"),
        .package(url: "https://github.com/apple/swift-openapi-urlsession", from: "1.0.2"),
        .package(url: "https://github.com/apple/swift-http-types", from: "1.0.2"),
    ],
    targets: [
        .target(
            name: "GolfProgressAPI",
            dependencies: [
                .product(name: "OpenAPIRuntime", package: "swift-openapi-runtime"),
                .product(name: "OpenAPIURLSession", package: "swift-openapi-urlsession"),
                .product(name: "HTTPTypes", package: "swift-http-types"),
            ],
            plugins: [
                .plugin(name: "OpenAPIGenerator", package: "swift-openapi-generator"),
            ]
        ),
        .testTarget(
            name: "GolfProgressAPITests",
            dependencies: ["GolfProgressAPI"]
        ),
    ]
)
