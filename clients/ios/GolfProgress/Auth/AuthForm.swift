import Foundation
import GolfProgressAPI
import Observation

enum AuthField: Hashable, CaseIterable {
    case displayName, email, password

    var label: String {
        switch self {
        case .displayName: String(localized: "Name")
        case .email: String(localized: "Email")
        case .password: String(localized: "Password")
        }
    }

    /// The API's key for this field in a validation problem.
    var apiName: String {
        switch self {
        case .displayName: "displayName"
        case .email: "email"
        case .password: "password"
        }
    }
}

/// The state of a sign-in form (#70): checks required fields and email shape locally,
/// then shows the API's per-field messages, and its 401/409/429 as a form error.
@MainActor
@Observable
final class AuthForm {
    let fields: [AuthField]
    /// What a 401 (Sign in) or 409 (Create account) means on this form.
    let rejectedMessage: String

    var displayName = ""
    var email = ""
    var password = ""

    private(set) var fieldErrors: [AuthField: String] = [:]
    private(set) var formError: String?
    private(set) var isSubmitting = false

    init(fields: [AuthField], rejectedMessage: String) {
        self.fields = fields
        self.rejectedMessage = rejectedMessage
    }

    /// Validates, then runs `action` with the trimmed values (the password as typed).
    /// Returns whether it succeeded.
    @discardableResult
    func submit(_ action: (_ email: String, _ displayName: String, _ password: String) async throws -> Void) async -> Bool {
        guard !isSubmitting else { return false }
        formError = nil
        fieldErrors = validate()
        guard fieldErrors.isEmpty else { return false }

        isSubmitting = true
        defer { isSubmitting = false }
        do {
            try await action(trimmed(email), trimmed(displayName), password)
            return true
        } catch {
            describe(error)
            return false
        }
    }

    private func validate() -> [AuthField: String] {
        var errors: [AuthField: String] = [:]
        for field in fields {
            let value = field == .password ? password : trimmed(value(of: field))
            if value.isEmpty {
                errors[field] = String(localized: "\(field.label) is required.")
            } else if field == .email, value.wholeMatch(of: /[^\s@]+@[^\s@]+\.[^\s@]+/) == nil {
                errors[field] = String(localized: "Enter a valid email address.")
            }
        }
        return errors
    }

    private func describe(_ error: Error) {
        guard let problem = APIProblem.from(error) else {
            formError = String(localized: "Couldn't reach GolfProgress. Check your connection and try again.")
            return
        }

        switch problem.status {
        case 400:
            for field in fields {
                if let message = problem.messages(for: field.apiName).first { fieldErrors[field] = message }
            }
            if fieldErrors.isEmpty { formError = String(localized: "Something went wrong. Please try again.") }
        case 401, 409:
            formError = rejectedMessage
        case 429:
            if let seconds = problem.retryAfterSeconds {
                formError = String(localized: "Too many attempts. Try again in \(seconds) seconds.")
            } else {
                formError = String(localized: "Too many attempts. Try again later.")
            }
        default:
            formError = String(localized: "Something went wrong. Please try again.")
        }
    }

    private func value(of field: AuthField) -> String {
        switch field {
        case .displayName: displayName
        case .email: email
        case .password: password
        }
    }

    private func trimmed(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
