// Open enums (#134): response fields the API may add values to at any time. The spec
// publishes them as plain strings, so a value this build doesn't know never fails a
// response; read them through these types, and show something generic for `.unknown`.
//
// The raw values mirror the spec's x-known-values; a test fails if they drift apart.
// All other enum fields are closed and typed directly by the generated client.

/// A practice session's type. `.unknown` for a type added to the API after this build.
public enum SessionType: String, Sendable, CaseIterable {
    case range = "Range", putting = "Putting", shortGame = "ShortGame", simulator = "Simulator"
    case lesson = "Lesson", other = "Other"
    case unknown

    public init(apiValue: String?) {
        self = apiValue.flatMap { Self(rawValue: $0) }.flatMap { $0 == .unknown ? nil : $0 } ?? .unknown
    }
}

/// A micro-win's category. `.unknown` for a category added to the API after this build.
public enum MicroWinCategory: String, Sendable, CaseIterable {
    case statDelta = "StatDelta", behavior = "Behavior", narrative = "Narrative", habit = "Habit"
    case recovery = "Recovery", focusArea = "FocusArea"
    case unknown

    public init(apiValue: String?) {
        self = apiValue.flatMap { Self(rawValue: $0) }.flatMap { $0 == .unknown ? nil : $0 } ?? .unknown
    }
}

/// Where a round came from. `.unknown` for a source added to the API after this build.
public enum RoundSource: String, Sendable, CaseIterable {
    case manual = "Manual", `import` = "Import", coach = "Coach", other = "Other"
    case unknown

    public init(apiValue: String?) {
        self = apiValue.flatMap { Self(rawValue: $0) }.flatMap { $0 == .unknown ? nil : $0 } ?? .unknown
    }
}

extension Components.Schemas.PracticeSessionResponse {
    public var knownSessionType: SessionType { SessionType(apiValue: sessionType) }
}

extension Components.Schemas.MicroWinResponse {
    public var knownCategory: MicroWinCategory { MicroWinCategory(apiValue: category) }
}

extension Components.Schemas.RoundResponse {
    public var knownSource: RoundSource { RoundSource(apiValue: source) }
}
