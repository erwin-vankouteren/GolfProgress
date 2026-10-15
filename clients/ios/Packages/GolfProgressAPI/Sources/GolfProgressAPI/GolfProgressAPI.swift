import Foundation
import OpenAPIRuntime
import OpenAPIURLSession

/// Entry point for the app (#64): a generated `Client` with the date handling and
/// middleware the API needs.
///
/// ```swift
/// let api = GolfProgressAPI.makeClient(serverURL: url, tokens: session)
/// let me = try await api.getMe().ok.body.json
/// ```
///
/// Calls return their success value, or throw: `APIProblem.from(error)` gives the API's
/// problem (status, message, per-field validation errors), and anything else is a
/// network or decoding error.
public enum GolfProgressAPI {
    public static func makeClient(
        serverURL: URL,
        tokens: AccessTokenProvider,
        transport: ClientTransport = URLSessionTransport()
    ) -> Client {
        Client(
            serverURL: serverURL,
            configuration: Configuration(dateTranscoder: DotNetDateTranscoder()),
            transport: transport,
            // The first is outermost: a 401 reaches BearerAuthMiddleware (which may
            // refresh and retry) before ProblemDetailsMiddleware turns what's left into
            // a thrown APIProblem.
            middlewares: [ProblemDetailsMiddleware(), BearerAuthMiddleware(tokens: tokens)]
        )
    }
}

/// Reads and writes `date-time` values the way the API writes them (#64).
///
/// .NET writes up to seven fractional-second digits (`2026-05-01T09:30:00.1234567+00:00`),
/// which the default ISO 8601 transcoder rejects. The fraction is cut to milliseconds
/// before parsing; nothing finer is ever needed.
public struct DotNetDateTranscoder: DateTranscoder {
    public init() {}

    public func encode(_ date: Date) throws -> String {
        Self.formatter(fractional: true).string(from: date)
    }

    public func decode(_ string: String) throws -> Date {
        let trimmed = Self.truncatingFraction(string)
        let hasFraction = trimmed.contains(".")
        guard let date = Self.formatter(fractional: hasFraction).date(from: trimmed) else {
            throw DecodingError.dataCorrupted(
                .init(codingPath: [], debugDescription: "Expected an ISO 8601 date-time, got '\(string)'."))
        }
        return date
    }

    /// "…:00.1234567+00:00" → "…:00.123+00:00"; strings without a fraction are unchanged.
    static func truncatingFraction(_ string: String) -> String {
        guard let dot = string.firstIndex(of: ".") else { return string }
        let digitsStart = string.index(after: dot)
        let digitsEnd = string[digitsStart...].firstIndex { !$0.isNumber } ?? string.endIndex
        let digits = string[digitsStart..<digitsEnd].prefix(3)
        let padded = digits.padding(toLength: 3, withPad: "0", startingAt: 0)
        return String(string[..<digitsStart]) + padded + String(string[digitsEnd...])
    }

    private static func formatter(fractional: Bool) -> ISO8601DateFormatter {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = fractional ? [.withInternetDateTime, .withFractionalSeconds] : [.withInternetDateTime]
        return formatter
    }
}
