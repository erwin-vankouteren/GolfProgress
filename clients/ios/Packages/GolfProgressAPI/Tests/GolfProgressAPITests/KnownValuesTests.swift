import Foundation
import HTTPTypes
import OpenAPIRuntime
import XCTest
@testable import GolfProgressAPI

/// Regression guard for #134: open enums never break this build when the API adds a value.
final class KnownValuesTests: XCTestCase {
    func testAValueAddedToTheAPIAfterThisBuildIsReadWithoutFailing() async throws {
        let api = GolfProgressAPI.makeClient(
            serverURL: URL(string: "https://api.example.test")!,
            tokens: NoTokens(),
            transport: StaticTransport(json: """
            {
              "practiceSessionId": "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a",
              "userId": "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
              "practicedAt": "2026-05-01",
              "durationMinutes": 45,
              "sessionType": "Fitness",
              "venueName": null,
              "tags": [],
              "primaryGoal": null,
              "notes": null,
              "createdAt": "2026-05-01T09:30:00+00:00"
            }
            """)
        )

        let session = try await api.getPracticeSession(path: .init(id: "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a")).ok.body.json

        XCTAssertEqual(session.sessionType, "Fitness")
        XCTAssertEqual(session.knownSessionType, .unknown)
    }

    func testKnownValuesMapToTheirCase() {
        XCTAssertEqual(SessionType(apiValue: "Putting"), .putting)
        XCTAssertEqual(MicroWinCategory(apiValue: "Habit"), .habit)
        XCTAssertEqual(RoundSource(apiValue: "Import"), .import)
        XCTAssertEqual(RoundSource(apiValue: nil), .unknown)
        // The fallback's own raw value isn't a real API value.
        XCTAssertEqual(SessionType(apiValue: "unknown"), .unknown)
    }

    /// So the build knows every value the API already returns.
    func testListsTheSameKnownValuesAsTheSpec() throws {
        XCTAssertEqual(try knownValues("PracticeSessionResponse", "sessionType"), apiValues(SessionType.self))
        XCTAssertEqual(try knownValues("MicroWinResponse", "category"), apiValues(MicroWinCategory.self))
        XCTAssertEqual(try knownValues("MonthlyReviewResponse", "topMicroWinCategory"), apiValues(MicroWinCategory.self))
        XCTAssertEqual(try knownValues("RoundResponse", "source"), apiValues(RoundSource.self))
    }

    private func apiValues<E: RawRepresentable & CaseIterable>(_: E.Type) -> [String]
    where E.RawValue == String, E: Equatable {
        E.allCases.filter { $0.rawValue != "unknown" }.map(\.rawValue)
    }

    /// Reads x-known-values from the package's copy of the spec (CI keeps it identical
    /// to api/openapi.json).
    private func knownValues(_ schema: String, _ property: String) throws -> [String] {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let data = try Data(contentsOf: packageRoot.appendingPathComponent("Sources/GolfProgressAPI/openapi.json"))
        let spec = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let schemas = try XCTUnwrap((spec["components"] as? [String: Any])?["schemas"] as? [String: Any])
        let properties = try XCTUnwrap((schemas[schema] as? [String: Any])?["properties"] as? [String: Any])
        return try XCTUnwrap((properties[property] as? [String: Any])?["x-known-values"] as? [String])
    }
}

private struct NoTokens: AccessTokenProvider {
    func accessToken() async -> String? { nil }
    func refreshAccessToken() async throws -> String? { nil }
}

/// Answers every request with the same JSON body.
private struct StaticTransport: ClientTransport {
    let json: String

    func send(
        _ request: HTTPRequest, body: HTTPBody?, baseURL: URL, operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        var response = HTTPResponse(status: .ok)
        response.headerFields[.contentType] = "application/json"
        return (response, HTTPBody(json))
    }
}
