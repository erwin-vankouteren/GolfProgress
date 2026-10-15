import Foundation
import GolfProgressAPI
import HTTPTypes
import OpenAPIRuntime
import Testing
@testable import GolfProgress

/// Tests for sign-in, session restore and the shared token refresh (#70), against a fake
/// API transport.
struct TokenRefresherTests {
    @Test func concurrentRefreshesShareOneCall() async throws {
        let store = InMemoryRefreshTokenStore(token: "refresh-1")
        let transport = FakeAuthTransport { operation, _ in
            guard operation == "refreshToken" else { return .problem(404) }
            try await Task.sleep(for: .milliseconds(100))
            return .auth(token: "access-2", refreshToken: "refresh-2")
        }
        let refresher = TokenRefresher(serverURL: server, store: store, transport: transport)

        let tokens = await withTaskGroup(of: String?.self) { group in
            for _ in 0..<5 { group.addTask { try? await refresher.refreshAccessToken() } }
            return await group.reduce(into: [String?]()) { $0.append($1) }
        }

        #expect(tokens == Array(repeating: "access-2", count: 5))
        #expect(transport.calls(to: "refreshToken") == 1)
        #expect(try store.load() == "refresh-2")
    }

    @Test func sendsTheStoredRefreshTokenAndKeepsTheRotatedOne() async throws {
        let store = InMemoryRefreshTokenStore(token: "refresh-1")
        let transport = FakeAuthTransport { _, _ in .auth(token: "access-2", refreshToken: "refresh-2") }
        let refresher = TokenRefresher(serverURL: server, store: store, transport: transport)

        let token = try await refresher.refreshAccessToken()

        #expect(token == "access-2")
        #expect(await refresher.accessToken() == "access-2")
        #expect(transport.refreshTokensSent(to: "refreshToken") == ["refresh-1"])
        #expect(try store.load() == "refresh-2")
    }

    @Test func aRejectedRefreshTokenEndsTheSession() async throws {
        let store = InMemoryRefreshTokenStore(token: "revoked")
        let transport = FakeAuthTransport { _, _ in .problem(401) }
        let refresher = TokenRefresher(serverURL: server, store: store, transport: transport)

        let token = try await refresher.refreshAccessToken()

        #expect(token == nil)
        #expect(try store.load() == nil)
    }

    @Test func anUnreachableAPIKeepsTheSession() async throws {
        let store = InMemoryRefreshTokenStore(token: "refresh-1")
        let transport = FakeAuthTransport { _, _ in throw URLError(.notConnectedToInternet) }
        let refresher = TokenRefresher(serverURL: server, store: store, transport: transport)

        await #expect(throws: (any Error).self) { try await refresher.refreshAccessToken() }
        #expect(try store.load() == "refresh-1")
    }

    @Test func withoutAStoredTokenThereIsNothingToRefresh() async throws {
        let transport = FakeAuthTransport { _, _ in .auth() }
        let refresher = TokenRefresher(serverURL: server, store: InMemoryRefreshTokenStore(), transport: transport)

        #expect(try await refresher.refreshAccessToken() == nil)
        #expect(transport.calls(to: "refreshToken") == 0)
    }
}

@MainActor
struct SessionTests {
    @Test func restoresFromTheStoredRefreshToken() async {
        let environment = makeEnvironment(store: InMemoryRefreshTokenStore(token: "refresh-1")) { _, _ in .auth() }

        await environment.session.restore()

        #expect(environment.session.state == .signedIn(userId: userId))
        #expect(await environment.session.accessToken() == "access-1")
    }

    @Test func withNothingStoredItIsSignedOut() async {
        let environment = makeEnvironment { _, _ in .auth() }

        await environment.session.restore()

        #expect(environment.session.state == .signedOut)
    }

    @Test func offlineAtLaunchKeepsTheStoredSessionToTryAgain() async throws {
        let store = InMemoryRefreshTokenStore(token: "refresh-1")
        let environment = makeEnvironment(store: store) { _, _ in throw URLError(.notConnectedToInternet) }

        await environment.session.restore()

        #expect(environment.session.state == .restoreFailed)
        #expect(try store.load() == "refresh-1")
    }

    @Test func signingInKeepsTheAccessTokenInMemoryAndTheRefreshTokenInTheStore() async throws {
        let store = InMemoryRefreshTokenStore()
        let environment = makeEnvironment(store: store) { _, _ in .auth(token: "access-1", refreshToken: "refresh-1") }

        try await environment.session.signIn(email: "erwin@example.test", password: "correct horse")

        #expect(environment.session.state == .signedIn(userId: userId))
        #expect(await environment.session.accessToken() == "access-1")
        #expect(try store.load() == "refresh-1")
    }

    @Test func signingOutRevokesAndForgetsTheSession() async throws {
        let store = InMemoryRefreshTokenStore()
        let transport = FakeAuthTransport { operation, _ in
            operation == "logout" ? .noContent : .auth(token: "access-1", refreshToken: "refresh-1")
        }
        let environment = AppEnvironment.make(serverURL: server, store: store, transport: transport, appVersion: "1.0.0")
        try await environment.session.signIn(email: "erwin@example.test", password: "correct horse")

        await environment.session.signOut()

        #expect(environment.session.state == .signedOut)
        #expect(await environment.session.accessToken() == nil)
        #expect(try store.load() == nil)
        #expect(transport.refreshTokensSent(to: "logout") == ["refresh-1"])
    }

    @Test func whenTheSessionCanNoLongerBeRefreshedTheAppSignsOut() async throws {
        let transport = FakeAuthTransport { operation, _ in
            switch operation {
            case "login": .auth()
            default: .problem(401) // GET /me with an expired token, then the refresh
            }
        }
        let environment = AppEnvironment.make(
            serverURL: server, store: InMemoryRefreshTokenStore(), transport: transport, appVersion: "1.0.0")
        try await environment.session.signIn(email: "erwin@example.test", password: "correct horse")

        _ = try? await environment.api.getMe()

        #expect(environment.session.state == .signedOut)
        #expect(transport.calls(to: "refreshToken") == 1)
    }

    private func makeEnvironment(
        store: InMemoryRefreshTokenStore = InMemoryRefreshTokenStore(),
        respond: @escaping @Sendable (String, String) async throws -> FakeReply
    ) -> AppEnvironment {
        AppEnvironment.make(serverURL: server, store: store, transport: FakeAuthTransport(respond), appVersion: "1.0.0")
    }
}

@MainActor
struct AuthFormTests {
    @Test func checksRequiredFieldsAndEmailBeforeCallingTheAPI() async {
        let form = AuthForm(fields: [.email, .password], rejectedMessage: "Rejected")
        var called = false

        await form.submit { _, _, _ in called = true }
        #expect(form.fieldErrors[.email] == "Email is required.")
        #expect(form.fieldErrors[.password] == "Password is required.")

        form.email = "not-an-email"
        await form.submit { _, _, _ in called = true }
        #expect(form.fieldErrors[.email] == "Enter a valid email address.")
        #expect(!called)
    }

    @Test func sendsTrimmedValuesButThePasswordAsTyped() async {
        let form = AuthForm(fields: [.displayName, .email, .password], rejectedMessage: "Rejected")
        form.displayName = " Erwin "
        form.email = " erwin@example.test\n"
        form.password = " secret "
        var sent: [String] = []

        let succeeded = await form.submit { email, displayName, password in sent = [email, displayName, password] }

        #expect(succeeded)
        #expect(sent == ["erwin@example.test", "Erwin", " secret "])
    }

    @Test func showsTheRejectedMessageFor401And409() async {
        for status in [401, 409] {
            let form = filledForm()
            await form.submit { _, _, _ in throw APIProblem(status: status) }
            #expect(form.formError == "Rejected")
        }
    }

    @Test func showsTheWaitAfterTooManyAttempts() async {
        let form = filledForm()

        await form.submit { _, _, _ in throw APIProblem(status: 429, retryAfterSeconds: 42) }

        #expect(form.formError == "Too many attempts. Try again in 42 seconds.")
    }

    @Test func putsValidationMessagesNextToTheirFields() async {
        let form = filledForm()

        await form.submit { _, _, _ in
            throw APIProblem(status: 400, errors: ["Password": ["Password must be at least 12 characters."]])
        }

        #expect(form.fieldErrors[.password] == "Password must be at least 12 characters.")
        #expect(form.formError == nil)
    }

    @Test func reportsAConnectionProblem() async {
        let form = filledForm()

        await form.submit { _, _, _ in throw URLError(.notConnectedToInternet) }

        #expect(form.formError == "Couldn't reach GolfProgress. Check your connection and try again.")
    }

    private func filledForm() -> AuthForm {
        let form = AuthForm(fields: [.email, .password], rejectedMessage: "Rejected")
        form.email = "erwin@example.test"
        form.password = "correct horse"
        return form
    }
}

struct KeychainRefreshTokenStoreTests {
    @Test func savesReplacesAndDeletesTheToken() throws {
        let store = KeychainRefreshTokenStore(service: "com.golfprogress.tests.\(UUID().uuidString)")
        defer { try? store.delete() }

        #expect(try store.load() == nil)
        try store.save("refresh-1")
        try store.save("refresh-2")
        #expect(try store.load() == "refresh-2")
        try store.delete()
        #expect(try store.load() == nil)
    }
}

// MARK: - Test doubles

private let server = URL(string: "https://api.example.test")!
private let userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f"

enum FakeReply {
    case auth(token: String = "access-1", refreshToken: String = "refresh-1")
    case problem(Int)
    case noContent
}

/// Answers each operation with `respond(operationID, requestBody)`, and records them.
final class FakeAuthTransport: ClientTransport, @unchecked Sendable {
    private let lock = NSLock()
    private var log: [(operation: String, body: String)] = []
    private let respond: @Sendable (String, String) async throws -> FakeReply

    init(_ respond: @escaping @Sendable (String, String) async throws -> FakeReply) {
        self.respond = respond
    }

    func calls(to operation: String) -> Int {
        lock.withLock { log.filter { $0.operation == operation }.count }
    }

    /// The `refreshToken` field of each request body sent to `operation`.
    func refreshTokensSent(to operation: String) -> [String?] {
        let bodies = lock.withLock { log.filter { $0.operation == operation }.map(\.body) }
        return bodies.map { body in
            let json = try? JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: Any]
            return json?["refreshToken"] as? String
        }
    }

    func send(
        _ request: HTTPRequest, body: HTTPBody?, baseURL: URL, operationID: String
    ) async throws -> (HTTPResponse, HTTPBody?) {
        var text = ""
        if let body { text = try await String(collecting: body, upTo: 1 << 16) }
        lock.withLock { log.append((operationID, text)) }

        switch try await respond(operationID, text) {
        case let .auth(token, refreshToken):
            var response = HTTPResponse(status: .ok)
            response.headerFields[.contentType] = "application/json"
            let json = #"{"userId":"\#(userId)","token":"\#(token)","refreshToken":"\#(refreshToken)","expiresAt":"2026-10-15T10:00:00Z"}"#
            return (response, HTTPBody(json))
        case let .problem(status):
            var response = HTTPResponse(status: .init(code: status))
            response.headerFields[.contentType] = "application/problem+json"
            return (response, HTTPBody(#"{"status":\#(status)}"#))
        case .noContent:
            return (HTTPResponse(status: .noContent), nil)
        }
    }
}
