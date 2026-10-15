// Generated from design/tokens.json by design/build-tokens.mjs (#73). Don't edit by hand.

import SwiftUI
import UIKit

enum DesignTokens {
    /// Colours that follow the system light/dark setting.
    enum Colors {
        static let background = SwiftUI.Color(light: 0xF7F8F5, dark: 0x121411)
        static let surface = SwiftUI.Color(light: 0xFFFFFF, dark: 0x1B1E1A)
        static let surfaceVariant = SwiftUI.Color(light: 0xEEF1EA, dark: 0x262A25)
        static let border = SwiftUI.Color(light: 0xD9DED4, dark: 0x3C4239)
        static let borderStrong = SwiftUI.Color(light: 0x6E786B, dark: 0x8C9688)
        static let textPrimary = SwiftUI.Color(light: 0x1B1F1A, dark: 0xE6EAE3)
        static let textSecondary = SwiftUI.Color(light: 0x4D564B, dark: 0xB5BDB0)
        static let primary = SwiftUI.Color(light: 0x1F6F43, dark: 0x7BD49B)
        static let onPrimary = SwiftUI.Color(light: 0xFFFFFF, dark: 0x003919)
        static let primaryContainer = SwiftUI.Color(light: 0xD4EDDC, dark: 0x1C5235)
        static let onPrimaryContainer = SwiftUI.Color(light: 0x0B3D21, dark: 0xBDF0CE)
        static let error = SwiftUI.Color(light: 0xB3261E, dark: 0xF2B8B5)
        static let onError = SwiftUI.Color(light: 0xFFFFFF, dark: 0x601410)
        static let success = SwiftUI.Color(light: 0x1E7B34, dark: 0x7CD992)
        static let warning = SwiftUI.Color(light: 0x8A5300, dark: 0xF5B65A)
        static let info = SwiftUI.Color(light: 0x1F5FA8, dark: 0x9CC4FF)
        static let pillarExecution = SwiftUI.Color(light: 0x2563EB, dark: 0x7AA7FF)
        static let pillarDamageControl = SwiftUI.Color(light: 0xC2410C, dark: 0xFF9A62)
        static let pillarConsistency = SwiftUI.Color(light: 0x7C3AED, dark: 0xB79BFF)
        static let pillarHabits = SwiftUI.Color(light: 0x15803D, dark: 0x6FD68F)
        static let confidenceLow = SwiftUI.Color(light: 0xA04E05, dark: 0xF5B65A)
        static let confidenceMedium = SwiftUI.Color(light: 0x1D4ED8, dark: 0x9CC4FF)
        static let confidenceHigh = SwiftUI.Color(light: 0x15803D, dark: 0x7CD992)
    }

    enum Spacing {
        static let xxs: CGFloat = 2
        static let xs: CGFloat = 4
        static let sm: CGFloat = 8
        static let md: CGFloat = 12
        static let lg: CGFloat = 16
        static let xl: CGFloat = 24
        static let xxl: CGFloat = 32
        static let xxxl: CGFloat = 48
    }

    enum Radius {
        static let sm: CGFloat = 4
        static let md: CGFloat = 8
        static let lg: CGFloat = 16
        static let full: CGFloat = 999
    }

    /// Text styles built on Dynamic Type, so they scale with the user's text size.
    enum Typography {
        static let display = Font.system(.largeTitle).weight(.bold)
        static let title = Font.system(.title).weight(.semibold)
        static let headline = Font.system(.title2).weight(.semibold)
        static let body = Font.system(.body).weight(.regular)
        static let callout = Font.system(.callout).weight(.regular)
        static let label = Font.system(.subheadline).weight(.medium)
        static let caption = Font.system(.footnote).weight(.regular)
    }

    static let touchTarget: CGFloat = 44
}

extension SwiftUI.Color {
    init(light: UInt32, dark: UInt32) {
        self.init(uiColor: UIColor { traits in
            UIColor(rgb: traits.userInterfaceStyle == .dark ? dark : light)
        })
    }
}

private extension UIColor {
    convenience init(rgb: UInt32) {
        self.init(
            red: CGFloat((rgb >> 16) & 0xFF) / 255,
            green: CGFloat((rgb >> 8) & 0xFF) / 255,
            blue: CGFloat(rgb & 0xFF) / 255,
            alpha: 1
        )
    }
}
