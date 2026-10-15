import Foundation
import GolfProgressAPI
import OpenAPIRuntime
import OpenAPIURLSession

/// Holds the tokens and talks to `/auth/*` (#70).
///
/// - The **access token** lives only in this actor's memory.
/// - The **refresh token** lives in the Keychain (`RefreshTokenStore`) and is read only
///   to send it to the API. The API rotates it on every refresh; the new one replaces it.
/// - Concurrent requests that hit a 401 share **one refresh**: the first starts it, the
///   rest await the same task. Refreshing twice would rotate the token under the first.
///
/// It calls the API through its own client without a token provider: the auth operations
/// are anonymous, and this keeps it independent of the client that uses it.
actor TokenRefresher {
    private let auth: Client
    private let store: RefreshTokenStore
    private var token: String?
    private var refreshing: Task<Components.Schemas.AuthResponse?, Error>?

    init(serverURL: URL, store: RefreshTokenStore, transport: ClientTransport = URLSessionTransport()) {
        self.auth = GolfProgressAPI.makeClient(serverURL: serverURL, tokens: SignedOut(), transport: transport)
        self.store = store
    }

    func accessToken() -> String? {
        token
    }

    /// A new access token, or nil when the session has ended (no refresh token, or the
    /// API rejected it). Throws when the API can't be reached: the session may still be
    /// valid, so nothing is forgotten.
    func refreshAccessToken() async throws -> String? {
        try await refresh()?.token
    }

    /// At launch: the signed-in user's ID from the stored refresh token, or nil.
    func restore() async throws -> String? {
        try await refresh()?.userId
    }

    /// Returns the user's ID. Throws an `APIProblem` for a wrong password (401),
    /// validation errors (400) or too many attempts (429).
    func signIn(email: String, password: String) async throws -> String {
        let response = try await auth.login(body: .json(.init(email: email, password: password))).ok.body.json
        return try accept(response)
    }

    /// Returns the user's ID. Throws an `APIProblem` for a taken email (409), validation
    /// errors (400) or too many attempts (429).
    func register(email: String, displayName: String, password: String) async throws -> String {
        let body = Components.Schemas.RegisterRequest(email: email, displayName: displayName, password: password)
        let response = try await auth.register(body: .json(body)).ok.body.json
        return try accept(response)
    }

    /// Forgets the session here first, then revokes the refresh token on the API. If the
    /// API can't be reached, the token simply expires there.
    func signOut() async {
        let stored = try? store.load()
        token = nil
        try? store.delete()

        if let stored {
            _ = try? await auth.logout(body: .json(.init(refreshToken: stored)))
        }
    }

    // MARK: Refreshing

    private func refresh() async throws -> Components.Schemas.AuthResponse? {
        if let refreshing { return try await refreshing.value }

        let task = Task { try await performRefresh() }
        refreshing = task
        defer { refreshing = nil }
        return try await task.value
    }

    private func performRefresh() async throws -> Components.Schemas.AuthResponse? {
        guard let stored = try store.load() else {
            token = nil
            return nil
        }

        do {
            let response = try await auth.refreshToken(body: .json(.init(refreshToken: stored))).ok.body.json
            _ = try accept(response)
            return response
        } catch let error where Self.isRejection(error) {
            // Expired or revoked: the user has to sign in again.
            token = nil
            try? store.delete()
            return nil
        }
    }

    /// The API refused the refresh token itself, as opposed to being unreachable,
    /// throttling (429) or failing (5xx).
    private static func isRejection(_ error: Error) -> Bool {
        guard let status = APIProblem.from(error)?.status else { return false }
        return status == 400 || status == 401
    }

    private func accept(_ response: Components.Schemas.AuthResponse) throws -> String {
        if let refreshToken = response.refreshToken {
            try store.save(refreshToken)
        }
        token = response.token
        return response.userId
    }
}
