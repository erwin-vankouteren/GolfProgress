import Foundation
import HTTPTypes
import OpenAPIRuntime
import XCTest
@testable import GolfProgressAPI

/// Tests for the generated client and the hand-written pieces around it (#64), run
/// against a fake transport so no server is needed.
final class GolfProgressAPITests: XCTestCase {
    private let server = URL(string: "https://api.example.test")!

    // MARK: Generated client, end to end

    func testGetMeDecodesTheUser() async throws {
        let transport = FakeTransport { request, _ in
            XCTAssertEqual(request.path, "/me")
            XCTAssertEqual(request.headerFields[.authorization], "Bearer token-1")
            return .json(200, Self.userJSON)
        }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: FakeTokens(), transport: transport)

        let me = try await api.getMe().ok.body.json

        XCTAssertEqual(me.displayName, "Erwin")
        XCTAssertEqual(me.units.rawValue, "Metric")
        XCTAssertEqual(me.goalFocus, ["putting"])
        XCTAssertNil(me.handicapIndex)
    }

    func testAnErrorResponseThrowsTheAPIProblem() async throws {
        let transport = FakeTransport { _, _ in
            .problem(404, #"{"title":"Not found","status":404,"detail":"User not found.","traceId":"00-abc"}"#)
        }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: FakeTokens(), transport: transport)

        do {
            _ = try await api.getMe()
            XCTFail("Expected an error")
        } catch {
            let problem = try XCTUnwrap(APIProblem.from(error))
            XCTAssertEqual(problem.status, 404)
            XCTAssertEqual(problem.detail, "User not found.")
            XCTAssertEqual(problem.traceId, "00-abc")
        }
    }

    func testValidationErrorsAreKeyedByField() async throws {
        let transport = FakeTransport { _, _ in
            .problem(400, #"{"status":400,"errors":{"PlayedAt":["PlayedAt must be an ISO date, yyyy-MM-dd."]}}"#)
        }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: FakeTokens(), transport: transport)

        do {
            _ = try await api.getMe()
            XCTFail("Expected an error")
        } catch {
            let problem = try XCTUnwrap(APIProblem.from(error))
            XCTAssertEqual(problem.messages(for: "playedAt"), ["PlayedAt must be an ISO date, yyyy-MM-dd."])
        }
    }

    // MARK: Bearer token and refresh

    func testA401RefreshesOnceAndRetriesWithTheNewToken() async throws {
        let tokens = FakeTokens(refreshed: "token-2")
        let transport = FakeTransport { request, attempt in
            attempt == 1
                ? .problem(401, #"{"status":401}"#)
                : (request.headerFields[.authorization] == "Bearer token-2" ? .json(200, Self.userJSON) : .problem(401, "{}"))
        }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: tokens, transport: transport)

        let me = try await api.getMe().ok.body.json

        XCTAssertEqual(me.displayName, "Erwin")
        let refreshes = await tokens.refreshCount
        XCTAssertEqual(refreshes, 1)
        XCTAssertEqual(transport.attempts, 2)
    }

    func testWhenRefreshingFailsTheOriginal401IsThrown() async throws {
        let tokens = FakeTokens(refreshed: nil)
        let transport = FakeTransport { _, _ in .problem(401, #"{"status":401,"title":"Unauthorized"}"#) }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: tokens, transport: transport)

        do {
            _ = try await api.getMe()
            XCTFail("Expected an error")
        } catch {
            XCTAssertEqual(APIProblem.from(error)?.status, 401)
        }
        XCTAssertEqual(transport.attempts, 1)
    }

    /// A wrong password answers 401 too; that must not be mistaken for an expired token.
    func testAnonymousOperationsCarryNoTokenAndNeverRefresh() async throws {
        let tokens = FakeTokens(refreshed: "token-2")
        let transport = FakeTransport { request, _ in
            XCTAssertNil(request.headerFields[.authorization])
            return .problem(401, #"{"status":401}"#)
        }
        let api = GolfProgressAPI.makeClient(serverURL: server, tokens: tokens, transport: transport)

        do {
            _ = try await api.login(body: .json(.init(email: "a@example.test", password: "wrong")))
            XCTFail("Expected an error")
        } catch {
            XCTAssertEqual(APIProblem.from(error)?.status, 401)
        }
        let refreshes = await tokens.refreshCount
        XCTAssertEqual(refreshes, 0)
    }

    // MARK: Dates

    func testDecodesDotNetDatesWithSevenFractionalDigits() throws {
        let transcoder = DotNetDateTranscoder()

        let precise = try transcoder.decode("2026-05-01T09:30:00.1234567+00:00")
        let plain = try transcoder.decode("2026-05-01T09:30:00+00:00")

        XCTAssertEqual(precise.timeIntervalSince(plain), 0.123, accuracy: 0.0005)
    }

    func testTruncatesOnlyTheFraction() {
        XCTAssertEqual(DotNetDateTranscoder.truncatingFraction("2026-05-01T09:30:00.1234567+02:00"), "2026-05-01T09:30:00.123+02:00")
        XCTAssertEqual(DotNetDateTranscoder.truncatingFraction("2026-05-01T09:30:00.5Z"), "2026-05-01T09:30:00.500Z")
        XCTAssertEqual(DotNetDateTranscoder.truncatingFraction("2026-05-01T09:30:00Z"), "2026-05-01T09:30:00Z")
    }

    // MARK: Fixtures

    static let userJSON = """
    {
      "userId": "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
      "email": "erwin@example.test",
      "displayName": "Erwin",
      "timezone": null,
      "units": "Metric",
      "trackingMode": "Minimal",
      "handicapIndex": null,
      "typical18Score": null,
      "goalFocus": ["putting"],
      "createdAt": "2026-05-01T09:30:00.1234567+00:00",
      "updatedAt": "2026-05-01T09:30:00+00:00"
    }
    """
}

// MARK: - Test doubles

private actor FakeTokens: AccessTokenProvider {
    private var current: String?
    private let refreshed: String?
    private(set) var refreshCount = 0

    init(current: String? = "token-1", refreshed: String? = "token-2") {
        self.current = current
        self.refreshed = refreshed
    }

    func accessToken() async -> String? { current }

    func refreshAccessToken() async throws -> String? {
        refreshCount += 1
        current = refreshed
        return refreshed
    }
}

private struct FakeResponse {
    let status: Int
    let contentType: String
    let body: String

    static func json(_ status: Int, _ body: String) -> FakeResponse {
        .init(status: status, contentType: "application/json; charset=utf-8", body: body)
    }

    static func problem(_ status: Int, _ body: String) -> FakeResponse {
        .init(status: status, contentType: "application/problem+json; charset=utf-8", body: body)
    }
}

/// Answers each request with `respond(request, attemptNumber)`.
private final class FakeTransport: ClientTransport, @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0
    private let respond: @Sendable (HTTPRequest, Int) -> FakeResponse

    init(_ respond: @escaping @Sendable (HTTPRequest, Int) -> FakeResponse) {
        self.respond = respond
    }

    var attempts: Int {
        lock.lock(); defer { lock.unlock() }
        return count
    }

    func send(
        _ request: HTTPRequest, body: HTTPBody?, baseURL: URL, operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        let attempt: Int = {
            lock.lock(); defer { lock.unlock() }
            count += 1
            return count
        }()

        let fake = respond(request, attempt)
        var response = HTTPResponse(status: .init(code: fake.status))
        response.headerFields[.contentType] = fake.contentType
        return (response, HTTPBody(fake.body))
    }
}
