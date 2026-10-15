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
/// minimum (#66, #76); otherwise the tabs. The check never blocks start-up.
struct RootView: View {
    @Environment(AppEnvironment.self) private var environment
    @State private var versionCheck = VersionCheck()

    var body: some View {
        Group {
            if versionCheck.state == .updateRequired {
                UpdateRequiredView()
            } else {
                MainTabView()
            }
        }
        .task { await versionCheck.run(api: environment.api, appVersion: environment.appVersion) }
    }
}
