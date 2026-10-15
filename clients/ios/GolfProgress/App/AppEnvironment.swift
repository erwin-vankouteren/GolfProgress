import Foundation
import GolfProgressAPI
import Observation

/// What the whole app shares (#76): the API client and this build's version. Injected
/// with `.environment(_:)`, so previews and tests can supply their own.
@MainActor
@Observable
final class AppEnvironment {
    let api: Client
    let appVersion: String

    init(api: Client, appVersion: String) {
        self.api = api
        self.appVersion = appVersion
    }

    /// The real app: the API URL and version come from Info.plist (project.yml).
    static func live(bundle: Bundle = .main) -> AppEnvironment {
        let baseURL = (bundle.object(forInfoDictionaryKey: "GolfProgressAPIBaseURL") as? String)
            .flatMap(URL.init(string:)) ?? URL(string: "http://localhost:5068")!
        let version = bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.0.0"

        return AppEnvironment(
            // Signed out until the session layer arrives in #70.
            api: GolfProgressAPI.makeClient(serverURL: baseURL, tokens: SignedOut()),
            appVersion: version
        )
    }
}

/// The token provider before anyone has signed in: no token, nothing to refresh.
struct SignedOut: AccessTokenProvider {
    func accessToken() async -> String? { nil }
    func refreshAccessToken() async throws -> String? { nil }
}
