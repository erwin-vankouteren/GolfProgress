import Foundation
import GolfProgressAPI
import Observation

/// The sign-in state the UI follows (#70), on top of `TokenRefresher`.
///
/// It's also the API client's `AccessTokenProvider`: when a refresh after a 401 finds the
/// session has ended, the state becomes `.signedOut` and the app returns to Welcome.
@MainActor
@Observable
final class Session: AccessTokenProvider {
    enum State: Equatable {
        /// Checking the Keychain's refresh token with the API, at launch.
        case restoring
        /// The API couldn't be reached at launch. The refresh token is kept, to try again.
        case restoreFailed
        case signedOut
        case signedIn(userId: String)
    }

    private(set) var state: State = .restoring
    nonisolated let tokens: TokenRefresher

    init(tokens: TokenRefresher) {
        self.tokens = tokens
    }

    /// At launch, and from "Try again" after `restoreFailed`.
    func restore() async {
        state = .restoring
        do {
            state = try await tokens.restore().map { .signedIn(userId: $0) } ?? .signedOut
        } catch {
            state = .restoreFailed
        }
    }

    func signIn(email: String, password: String) async throws {
        let userId = try await tokens.signIn(email: email, password: password)
        state = .signedIn(userId: userId)
    }

    func register(email: String, displayName: String, password: String) async throws {
        let userId = try await tokens.register(email: email, displayName: displayName, password: password)
        state = .signedIn(userId: userId)
    }

    func signOut() async {
        await tokens.signOut()
        state = .signedOut
    }

    // MARK: AccessTokenProvider

    nonisolated func accessToken() async -> String? {
        await tokens.accessToken()
    }

    nonisolated func refreshAccessToken() async throws -> String? {
        let token = try await tokens.refreshAccessToken()
        if token == nil { await sessionEnded() }
        return token
    }

    private func sessionEnded() {
        if case .signedIn = state { state = .signedOut }
    }
}
