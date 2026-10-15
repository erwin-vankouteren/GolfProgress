import Foundation
import GolfProgressAPI
import Observation
import SwiftUI

/// Compares two major.minor.patch versions numerically: negative when `a` is older, zero
/// when equal, positive when newer. "1.10.0" is newer than "1.9.0". Unparseable parts
/// count as 0, so a malformed version never blocks the app.
func compareVersions(_ a: String, _ b: String) -> Int {
    let parts = { (version: String) in version.split(separator: ".").map { Int($0) ?? 0 } }
    let (x, y) = (parts(a), parts(b))

    for i in 0..<max(x.count, y.count) {
        let diff = (i < x.count ? x[i] : 0) - (i < y.count ? y[i] : 0)
        if diff != 0 { return diff }
    }
    return 0
}

/// Checks this build against GET /client-config's minimum iOS version (#66, #76).
///
/// It never blocks start-up: the app shows while the check runs, and if it fails
/// (offline, API down) the app carries on. Only a definite "too old" stops it.
@MainActor
@Observable
final class VersionCheck {
    enum State: Equatable { case checking, supported, updateRequired }

    private(set) var state: State = .checking

    func run(api: some APIProtocol, appVersion: String) async {
        do {
            let config = try await api.getClientConfig().ok.body.json
            state = compareVersions(appVersion, config.minimumVersions.ios) < 0 ? .updateRequired : .supported
        } catch {
            state = .supported
        }
    }
}

struct UpdateRequiredView: View {
    var body: some View {
        VStack(spacing: DesignTokens.Spacing.lg) {
            Text("Update required")
                .font(DesignTokens.Typography.title)
                .foregroundStyle(DesignTokens.Colors.textPrimary)
            Text("This version of GolfProgress is no longer supported. Update the app to keep using it.")
                .font(DesignTokens.Typography.body)
                .foregroundStyle(DesignTokens.Colors.textSecondary)
                .multilineTextAlignment(.center)
        }
        .padding(DesignTokens.Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(DesignTokens.Colors.background)
    }
}
