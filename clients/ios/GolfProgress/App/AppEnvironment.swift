import Foundation
import GolfProgressAPI
import Observation
import OpenAPIRuntime
import OpenAPIURLSession

/// What the whole app shares (#76, #70): the API client, the session and this build's
/// version. Injected with `.environment(_:)`, so previews and tests can supply their own.
@MainActor
@Observable
final class AppEnvironment {
    let api: Client
    let session: Session
    let appVersion: String

    init(api: Client, session: Session, appVersion: String) {
        self.api = api
        self.session = session
        self.appVersion = appVersion
    }

    /// The API client and session over one transport. The client gets its tokens from the
    /// session, and returns to Welcome when the session can't be refreshed.
    static func make(
        serverURL: URL,
        store: RefreshTokenStore,
        transport: ClientTransport = URLSessionTransport(),
        appVersion: String
    ) -> AppEnvironment {
        let session = Session(tokens: TokenRefresher(serverURL: serverURL, store: store, transport: transport))
        let api = GolfProgressAPI.makeClient(serverURL: serverURL, tokens: session, transport: transport)
        return AppEnvironment(api: api, session: session, appVersion: appVersion)
    }

    /// The real app: the API URL and version come from Info.plist (project.yml), and the
    /// refresh token lives in the Keychain.
    static func live(bundle: Bundle = .main) -> AppEnvironment {
        let baseURL = (bundle.object(forInfoDictionaryKey: "GolfProgressAPIBaseURL") as? String)
            .flatMap(URL.init(string:)) ?? URL(string: "http://localhost:5068")!
        let version = bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.0.0"

        return make(serverURL: baseURL, store: KeychainRefreshTokenStore(), appVersion: version)
    }

    /// For previews: nothing stored, and an API that isn't there.
    static func preview() -> AppEnvironment {
        make(serverURL: URL(string: "http://localhost:5068")!, store: InMemoryRefreshTokenStore(), appVersion: "1.0.0")
    }
}

/// The token provider before anyone has signed in: no token, nothing to refresh. Also
/// used by clients that only make anonymous calls.
struct SignedOut: AccessTokenProvider {
    func accessToken() async -> String? { nil }
    func refreshAccessToken() async throws -> String? { nil }
}
