import SwiftUI

@main
struct GolfProgressApp: App {
    @State private var environment = AppEnvironment.live()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(environment)
        }
    }
}

/// Shows "Update required" instead of the app when this build is older than the API's
/// minimum (#66, #76). Otherwise: the tabs when signed in, Welcome when not (#70). The
/// version check never blocks start-up.
struct RootView: View {
    @Environment(AppEnvironment.self) private var environment
    @State private var versionCheck = VersionCheck()

    var body: some View {
        Group {
            if versionCheck.state == .updateRequired {
                UpdateRequiredView()
            } else {
                switch environment.session.state {
                case .restoring:
                    ProgressView("Signing you in…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(DesignTokens.Colors.background)
                case .restoreFailed:
                    RestoreFailedView { Task { await environment.session.restore() } }
                case .signedOut:
                    AuthFlowView()
                case .signedIn:
                    MainTabView()
                }
            }
        }
        .task { await versionCheck.run(api: environment.api, appVersion: environment.appVersion) }
        .task { await environment.session.restore() }
    }
}

/// The API couldn't be reached at launch. The stored session is kept, so trying again
/// once online signs straight back in.
struct RestoreFailedView: View {
    let retry: () -> Void

    var body: some View {
        VStack(spacing: DesignTokens.Spacing.lg) {
            Text("Couldn't reach GolfProgress. Check your connection and try again.")
                .font(DesignTokens.Typography.body)
                .foregroundStyle(DesignTokens.Colors.textSecondary)
                .multilineTextAlignment(.center)
            Button("Try again", action: retry)
                .buttonStyle(.borderedProminent)
                .tint(DesignTokens.Colors.primary)
                .frame(minHeight: DesignTokens.touchTarget)
        }
        .padding(DesignTokens.Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(DesignTokens.Colors.background)
    }
}
