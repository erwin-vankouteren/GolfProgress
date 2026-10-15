import SwiftUI

/// Welcome, Create account and Sign in (docs/clients/wireframes.md → "Authentication",
/// #70). Shown whenever nobody is signed in.
enum AuthRoute: Hashable {
    case signIn, createAccount
}

struct AuthFlowView: View {
    @State private var path: [AuthRoute] = []

    var body: some View {
        NavigationStack(path: $path) {
            WelcomeView(path: $path)
                .navigationDestination(for: AuthRoute.self) { route in
                    switch route {
                    case .signIn: SignInView(path: $path)
                    case .createAccount: CreateAccountView(path: $path)
                    }
                }
        }
        .tint(DesignTokens.Colors.primary)
    }
}

struct WelcomeView: View {
    @Binding var path: [AuthRoute]

    var body: some View {
        AuthScreen {
            Text("GolfProgress")
                .font(DesignTokens.Typography.display)
                .foregroundStyle(DesignTokens.Colors.textPrimary)
                .accessibilityAddTraits(.isHeader)
            Text("See your progress beyond the score: every round scored on execution, damage control, consistency and habits.")
                .font(DesignTokens.Typography.body)
                .foregroundStyle(DesignTokens.Colors.textSecondary)
            PrimaryButton(title: "Create account", isBusy: false) { path.append(.createAccount) }
            Button("Sign in") { path.append(.signIn) }
                .buttonStyle(.bordered)
                .frame(maxWidth: .infinity, minHeight: DesignTokens.touchTarget)
        }
    }
}

struct SignInView: View {
    @Binding var path: [AuthRoute]
    @Environment(AppEnvironment.self) private var environment
    @State private var form = AuthForm(fields: [.email, .password], rejectedMessage: String(localized: "Email or password is wrong."))

    var body: some View {
        AuthScreen {
            AuthTextField(field: .email, text: $form.email, error: form.fieldErrors[.email], contentType: .username)
            AuthTextField(field: .password, text: $form.password, error: form.fieldErrors[.password], contentType: .password)
            FormErrorText(message: form.formError)
            PrimaryButton(title: "Sign in", isBusy: form.isSubmitting) { submit() }
            Button("New here? Create an account") { path = [.createAccount] }
                .frame(minHeight: DesignTokens.touchTarget)
        }
        .navigationTitle("Sign in")
        .onSubmit(submit)
    }

    private func submit() {
        Task {
            await form.submit { email, _, password in
                try await environment.session.signIn(email: email, password: password)
            }
        }
    }
}

struct CreateAccountView: View {
    @Binding var path: [AuthRoute]
    @Environment(AppEnvironment.self) private var environment
    @State private var form = AuthForm(
        fields: [.displayName, .email, .password],
        rejectedMessage: String(localized: "An account with this email already exists. Sign in instead.")
    )

    var body: some View {
        AuthScreen {
            AuthTextField(field: .displayName, text: $form.displayName, error: form.fieldErrors[.displayName], contentType: .name)
            AuthTextField(field: .email, text: $form.email, error: form.fieldErrors[.email], contentType: .username)
            AuthTextField(field: .password, text: $form.password, error: form.fieldErrors[.password], contentType: .newPassword)
            FormErrorText(message: form.formError)
            PrimaryButton(title: "Create account", isBusy: form.isSubmitting) { submit() }
            Button("Already have an account? Sign in") { path = [.signIn] }
                .frame(minHeight: DesignTokens.touchTarget)
        }
        .navigationTitle("Create account")
        .onSubmit(submit)
    }

    private func submit() {
        Task {
            await form.submit { email, displayName, password in
                try await environment.session.register(email: email, displayName: displayName, password: password)
            }
        }
    }
}

// MARK: - Pieces

private struct AuthScreen<Content: View>: View {
    @ViewBuilder let content: () -> Content

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesignTokens.Spacing.lg) {
                content()
            }
            .padding(DesignTokens.Spacing.xl)
            .frame(maxWidth: 520)
            .frame(maxWidth: .infinity)
        }
        .scrollDismissesKeyboard(.interactively)
        .background(DesignTokens.Colors.background)
    }
}

/// A labelled field with its error underneath. `contentType` drives password AutoFill:
/// `.username` + `.password` to sign in, `.newPassword` to suggest a strong one.
private struct AuthTextField: View {
    let field: AuthField
    @Binding var text: String
    let error: String?
    let contentType: UITextContentType

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.Spacing.xs) {
            Text(field.label)
                .font(DesignTokens.Typography.label)
                .foregroundStyle(DesignTokens.Colors.textPrimary)
                .accessibilityHidden(true)
            input
                .textContentType(contentType)
                .font(DesignTokens.Typography.body)
                .padding(.horizontal, DesignTokens.Spacing.md)
                .frame(minHeight: DesignTokens.touchTarget)
                .background(DesignTokens.Colors.surface, in: RoundedRectangle(cornerRadius: DesignTokens.Radius.md))
                .overlay(
                    RoundedRectangle(cornerRadius: DesignTokens.Radius.md)
                        .stroke(error == nil ? DesignTokens.Colors.borderStrong : DesignTokens.Colors.error)
                )
                .accessibilityLabel(Text(field.label))
                .accessibilityHint(Text(error ?? ""))
            if let error {
                Text(error)
                    .font(DesignTokens.Typography.caption)
                    .foregroundStyle(DesignTokens.Colors.error)
                    .accessibilityHidden(true)
            }
        }
    }

    @ViewBuilder private var input: some View {
        switch field {
        case .password:
            SecureField(text: $text) { Text(field.label) }
        case .email:
            TextField(text: $text) { Text(field.label) }
                .keyboardType(.emailAddress)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
        case .displayName:
            TextField(text: $text) { Text(field.label) }
                .textInputAutocapitalization(.words)
        }
    }
}

private struct FormErrorText: View {
    let message: String?

    var body: some View {
        if let message {
            Text(message)
                .font(DesignTokens.Typography.callout)
                .foregroundStyle(DesignTokens.Colors.error)
                .fixedSize(horizontal: false, vertical: true)
                .onAppear { AccessibilityNotification.Announcement(message).post() }
                .onChange(of: message) { _, new in AccessibilityNotification.Announcement(new).post() }
        }
    }
}

private struct PrimaryButton: View {
    let title: LocalizedStringKey
    let isBusy: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Group {
                if isBusy {
                    ProgressView().accessibilityLabel(Text("Please wait…"))
                } else {
                    Text(title)
                }
            }
            .frame(maxWidth: .infinity, minHeight: DesignTokens.touchTarget)
        }
        .buttonStyle(.borderedProminent)
        .disabled(isBusy)
    }
}

// Accessibility checks (#78): the forms at the largest text size, light and dark.
#Preview("Welcome, largest text") {
    AuthFlowView()
        .environment(AppEnvironment.preview())
        .environment(\.dynamicTypeSize, .accessibility5)
}

#Preview("Sign in, dark") {
    NavigationStack { SignInView(path: .constant([.signIn])) }
        .environment(AppEnvironment.preview())
        .preferredColorScheme(.dark)
}
