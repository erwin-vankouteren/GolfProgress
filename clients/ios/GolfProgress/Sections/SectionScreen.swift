import SwiftUI

/// The sections behind the tabs, with their empty states from the wireframes (#76). The
/// features replace these in M2.
enum AppSection {
    case home, rounds, practice, progress

    var title: LocalizedStringKey {
        switch self {
        case .home: "Home"
        case .rounds: "Rounds"
        case .practice: "Practice"
        case .progress: "Progress"
        }
    }

    var emptyMessage: LocalizedStringKey {
        switch self {
        case .home: "Log your first round to see your fitness score."
        case .rounds: "No rounds logged yet."
        case .practice: "No practice logged yet. Practice counts towards your Habits score."
        case .progress: "Log a few rounds to see your trends."
        }
    }

    var offersLogRound: Bool { self == .home || self == .rounds }
}

struct SectionScreen: View {
    let section: AppSection
    let router: TabRouter

    var body: some View {
        NavigationStack {
            ScrollView {
                EmptyStateView(message: section.emptyMessage) {
                    if section.offersLogRound {
                        Button("Log round") { router.select(.logRound) }
                            .buttonStyle(.borderedProminent)
                            .tint(DesignTokens.Colors.primary)
                    }
                }
                .padding(DesignTokens.Spacing.lg)
            }
            .background(DesignTokens.Colors.background)
            .navigationTitle(section.title)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        router.isShowingProfile = true
                    } label: {
                        Image(systemName: "person.crop.circle")
                            .font(DesignTokens.Typography.headline)
                            .frame(minWidth: DesignTokens.touchTarget, minHeight: DesignTokens.touchTarget)
                    }
                    .accessibilityLabel(Text("Profile"))
                }
            }
        }
    }
}

struct EmptyStateView<Action: View>: View {
    let message: LocalizedStringKey
    @ViewBuilder let action: () -> Action

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.Spacing.md) {
            Text(message)
                .font(DesignTokens.Typography.body)
                .foregroundStyle(DesignTokens.Colors.textSecondary)
                .fixedSize(horizontal: false, vertical: true)
            action()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(DesignTokens.Spacing.xl)
        .background(DesignTokens.Colors.surface, in: RoundedRectangle(cornerRadius: DesignTokens.Radius.lg))
        .overlay(RoundedRectangle(cornerRadius: DesignTokens.Radius.lg).stroke(DesignTokens.Colors.border))
    }
}

struct LogRoundView: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            EmptyStateView(message: "The round form comes next.") { EmptyView() }
                .padding(DesignTokens.Spacing.lg)
                .frame(maxHeight: .infinity, alignment: .top)
                .background(DesignTokens.Colors.background)
                .navigationTitle("Log round")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("Close") { dismiss() }
                    }
                }
        }
    }
}

struct ProfileView: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            EmptyStateView(message: "Your profile and preferences will appear here.") { EmptyView() }
                .padding(DesignTokens.Spacing.lg)
                .frame(maxHeight: .infinity, alignment: .top)
                .background(DesignTokens.Colors.background)
                .navigationTitle("Profile")
                .toolbar {
                    ToolbarItem(placement: .confirmationAction) {
                        Button("Done") { dismiss() }
                    }
                }
        }
    }
}
