import Observation
import SwiftUI

/// The tabs (docs/clients/ux-design.md → "Navigation"). Profile isn't a tab: it opens
/// from the avatar in each screen's top bar, so the centre Log round button fits.
enum AppTab: Hashable, CaseIterable {
    case home, rounds, logRound, practice, progress
}

/// Tab selection (#76). Choosing Log round opens it as a sheet and leaves the current tab
/// selected, so closing the sheet returns to where the user was.
@MainActor
@Observable
final class TabRouter {
    var selection: AppTab = .home
    var isLoggingRound = false
    var isShowingProfile = false

    func select(_ tab: AppTab) {
        if tab == .logRound {
            isLoggingRound = true
        } else {
            selection = tab
        }
    }
}

struct MainTabView: View {
    @State private var router = TabRouter()

    var body: some View {
        TabView(selection: Binding(get: { router.selection }, set: { router.select($0) })) {
            SectionScreen(section: .home, router: router)
                .tabItem { Label("Home", systemImage: "house") }
                .tag(AppTab.home)

            SectionScreen(section: .rounds, router: router)
                .tabItem { Label("Rounds", systemImage: "list.bullet") }
                .tag(AppTab.rounds)

            // Never shown: selecting it opens the Log round sheet instead.
            Color.clear
                .tabItem { Label("Log round", systemImage: "plus.circle.fill") }
                .tag(AppTab.logRound)

            SectionScreen(section: .practice, router: router)
                .tabItem { Label("Practice", systemImage: "figure.golf") }
                .tag(AppTab.practice)

            SectionScreen(section: .progress, router: router)
                .tabItem { Label("Progress", systemImage: "chart.line.uptrend.xyaxis") }
                .tag(AppTab.progress)
        }
        .tint(DesignTokens.Colors.primary)
        .sheet(isPresented: Binding(get: { router.isLoggingRound }, set: { router.isLoggingRound = $0 })) {
            LogRoundView()
        }
        .sheet(isPresented: Binding(get: { router.isShowingProfile }, set: { router.isShowingProfile = $0 })) {
            ProfileView()
        }
    }
}
