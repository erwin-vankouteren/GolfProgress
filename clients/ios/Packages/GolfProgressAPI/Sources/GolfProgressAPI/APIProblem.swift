import Foundation
import OpenAPIRuntime

/// An error response from the API, decoded from its ProblemDetails body (#64).
///
/// Every API error is `application/problem+json`. `ProblemDetailsMiddleware` turns such a
/// response into this error, so a call either returns its success value or throws:
/// views never need to switch over each operation's error cases.
public struct APIProblem: Error, Equatable, Sendable, Decodable {
    /// HTTP status code, e.g. 400, 404, 409, 429, 500.
    public var status: Int
    public var title: String?
    public var detail: String?
    /// Matches the API's log entry for this request. Show it in debug builds only.
    public var traceId: String?
    /// For 400s: validation messages keyed by field, to show next to that field.
    public var errors: [String: [String]]
    /// For 429s: seconds to wait before trying again, from the Retry-After header. Set by
    /// `ProblemDetailsMiddleware`; not part of the body.
    public var retryAfterSeconds: Int?

    public init(
        status: Int, title: String? = nil, detail: String? = nil,
        traceId: String? = nil, errors: [String: [String]] = [:], retryAfterSeconds: Int? = nil
    ) {
        self.status = status
        self.title = title
        self.detail = detail
        self.traceId = traceId
        self.errors = errors
        self.retryAfterSeconds = retryAfterSeconds
    }

    private enum CodingKeys: String, CodingKey {
        case status, title, detail, traceId, errors
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        status = try container.decodeIfPresent(Int.self, forKey: .status) ?? 0
        title = try container.decodeIfPresent(String.self, forKey: .title)
        detail = try container.decodeIfPresent(String.self, forKey: .detail)
        traceId = try container.decodeIfPresent(String.self, forKey: .traceId)
        errors = try container.decodeIfPresent([String: [String]].self, forKey: .errors) ?? [:]
    }

    /// The validation messages for one field, matched case-insensitively (the API keys
    /// body fields as `PlayedAt` and query parameters as `from`).
    public func messages(for field: String) -> [String] {
        errors.first { $0.key.caseInsensitiveCompare(field) == .orderedSame }?.value ?? []
    }

    /// The API problem behind an error thrown by a client call, if it was one. The
    /// generated client wraps middleware errors in `ClientError`.
    public static func from(_ error: Error) -> APIProblem? {
        if let problem = error as? APIProblem { return problem }
        if let clientError = error as? ClientError { return clientError.underlyingError as? APIProblem }
        return nil
    }
}
