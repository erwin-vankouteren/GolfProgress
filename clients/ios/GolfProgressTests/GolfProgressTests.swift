import Foundation
import GolfProgressAPI
import HTTPTypes
import OpenAPIRuntime
import Testing
@testable import GolfProgress

/// Tests for the app shell's logic (#76).
struct VersionComparisonTests {
    @Test(arguments: [
        ("1.0.0", "1.0.0", 0),
        ("1.0.0", "1.0.1", -1),
        ("1.10.0", "1.9.0", 1),
        ("2.0.0", "1.99.99", 1),
        ("1.0", "1.0.0", 0),
    ])
    func comparesNumerically(a: String, b: String, sign: Int) {
        #expect(compareVersions(a, b).signum() == sign)
    }
}

@MainActor
struct VersionCheckTests {
    @Test func olderThanTheMinimumRequiresAnUpdate() async {
        let check = VersionCheck()
        await check.run(api: client(minimumIOS: "1.2.0"), appVersion: "1.0.0")
        #expect(check.state == .updateRequired)
    }

    @Test func meetingTheMinimumIsSupported() async {
        let check = VersionCheck()
        await check.run(api: client(minimumIOS: "1.2.0"), appVersion: "1.2.0")
        #expect(check.state == .supported)
    }

    @Test func aFailedCheckNeverBlocksTheApp() async {
        let check = VersionCheck()
        await check.run(api: client(minimumIOS: nil), appVersion: "1.0.0")
        #expect(check.state == .supported)
    }

    /// A client whose GET /client-config answers with `minimumIOS`, or a 500 when nil.
    private func client(minimumIOS: String?) -> Client {
        GolfProgressAPI.makeClient(
            serverURL: URL(string: "https://api.example.test")!,
            tokens: SignedOut(),
            transport: ConfigTransport(minimumIOS: minimumIOS)
        )
    }
}

@MainActor
struct TabRouterTests {
    @Test func choosingLogRoundOpensItAndKeepsTheCurrentTab() {
        let router = TabRouter()
        router.select(.rounds)

        router.select(.logRound)

        #expect(router.isLoggingRound)
        #expect(router.selection == .rounds)
    }

    @Test func choosingASectionSelectsIt() {
        let router = TabRouter()

        router.select(.progress)

        #expect(router.selection == .progress)
        #expect(!router.isLoggingRound)
    }
}

private struct ConfigTransport: ClientTransport {
    let minimumIOS: String?

    func send(
        _ request: HTTPRequest, body: HTTPBody?, baseURL: URL, operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        guard let minimumIOS else {
            var failed = HTTPResponse(status: .internalServerError)
            failed.headerFields[.contentType] = "application/problem+json"
            return (failed, HTTPBody(#"{"status":500}"#))
        }
        var response = HTTPResponse(status: .ok)
        response.headerFields[.contentType] = "application/json"
        let json = #"{"minimumVersions":{"ios":"\#(minimumIOS)","android":"1.0.0","web":"1.0.0"}}"#
        return (response, HTTPBody(json))
    }
}
