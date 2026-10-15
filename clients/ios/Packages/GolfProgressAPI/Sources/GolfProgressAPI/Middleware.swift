import Foundation
import HTTPTypes
import OpenAPIRuntime

/// Supplies access tokens to `BearerAuthMiddleware` (#64).
///
/// The session layer (#70) implements this: it keeps the access token in memory and the
/// refresh token in the Keychain, and makes sure concurrent callers share a single
/// refresh rather than each starting their own.
public protocol AccessTokenProvider: Sendable {
    /// The current access token, or nil when signed out.
    func accessToken() async -> String?

    /// Exchanges the refresh token for a new access token after a 401. Returns nil when
    /// the session can't be refreshed and the user must sign in again.
    func refreshAccessToken() async throws -> String?
}

/// Adds `Authorization: Bearer` to authenticated operations, and on a 401 refreshes the
/// token once and retries the request (#64).
public struct BearerAuthMiddleware: ClientMiddleware {
    /// Operations reached before signing in. They never carry a token, and a 401 from
    /// them (such as a wrong password on login) must not trigger a refresh.
    public static let anonymousOperations: Set<String> = [
        "register", "login", "refreshToken", "logout", "getClientConfig",
    ]

    /// Request bodies are buffered so the request can be sent again after a refresh. API
    /// request bodies are small JSON documents, far below this.
    static let maxBufferedBodyBytes = 1024 * 1024

    let tokens: AccessTokenProvider

    public init(tokens: AccessTokenProvider) {
        self.tokens = tokens
    }

    public func intercept(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String,
        next: @Sendable (HTTPRequest, HTTPBody?, URL) async throws -> (HTTPResponse, HTTPBody?)
    ) async throws -> (HTTPResponse, HTTPBody?) {
        guard !Self.anonymousOperations.contains(operationID),
              let token = await tokens.accessToken()
        else {
            return try await next(request, body, baseURL)
        }

        var bufferedBody: Data?
        if let body {
            bufferedBody = try await Data(collecting: body, upTo: Self.maxBufferedBodyBytes)
        }

        let (response, responseBody) = try await next(authorized(request, token), bufferedBody.map { HTTPBody($0) }, baseURL)
        guard response.status == .unauthorized else { return (response, responseBody) }

        // Expired token: refresh once and retry. If refreshing fails, the original 401
        // stands and the app returns to sign-in.
        guard let refreshed = try await tokens.refreshAccessToken() else {
            return (response, responseBody)
        }
        return try await next(authorized(request, refreshed), bufferedBody.map { HTTPBody($0) }, baseURL)
    }

    private func authorized(_ request: HTTPRequest, _ token: String) -> HTTPRequest {
        var request = request
        request.headerFields[.authorization] = "Bearer \(token)"
        return request
    }
}

/// Turns every `application/problem+json` response into a thrown `APIProblem` (#64).
public struct ProblemDetailsMiddleware: ClientMiddleware {
    static let maxProblemBytes = 64 * 1024

    public init() {}

    public func intercept(
        _ request: HTTPRequest,
        body: HTTPBody?,
        baseURL: URL,
        operationID: String,
        next: @Sendable (HTTPRequest, HTTPBody?, URL) async throws -> (HTTPResponse, HTTPBody?)
    ) async throws -> (HTTPResponse, HTTPBody?) {
        let (response, responseBody) = try await next(request, body, baseURL)

        let contentType = response.headerFields[.contentType] ?? ""
        guard response.status.code >= 400, contentType.lowercased().hasPrefix("application/problem+json") else {
            return (response, responseBody)
        }

        var problem = APIProblem(status: response.status.code, title: response.status.reasonPhrase)
        if let responseBody,
           let data = try? await Data(collecting: responseBody, upTo: Self.maxProblemBytes),
           let decoded = try? JSONDecoder().decode(APIProblem.self, from: data) {
            problem = decoded
            if problem.status == 0 { problem.status = response.status.code }
        }
        throw problem
    }
}
