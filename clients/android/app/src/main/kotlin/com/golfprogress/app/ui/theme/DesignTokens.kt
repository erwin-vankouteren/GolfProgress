// Generated from design/tokens.json by design/build-tokens.mjs (#73). Don't edit by hand.

package com.golfprogress.app.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

object DesignTokens {
    /** The colours of one theme; [Light] and [Dark] have the same names. */
    interface Palette {
        val background: Color
        val surface: Color
        val surfaceVariant: Color
        val border: Color
        val borderStrong: Color
        val textPrimary: Color
        val textSecondary: Color
        val primary: Color
        val onPrimary: Color
        val primaryContainer: Color
        val onPrimaryContainer: Color
        val error: Color
        val onError: Color
        val success: Color
        val warning: Color
        val info: Color
        val pillarExecution: Color
        val pillarDamageControl: Color
        val pillarConsistency: Color
        val pillarHabits: Color
        val confidenceLow: Color
        val confidenceMedium: Color
        val confidenceHigh: Color
    }

    object Light : Palette {
        override val background = Color(0xFFF7F8F5)
        override val surface = Color(0xFFFFFFFF)
        override val surfaceVariant = Color(0xFFEEF1EA)
        override val border = Color(0xFFD9DED4)
        override val borderStrong = Color(0xFF6E786B)
        override val textPrimary = Color(0xFF1B1F1A)
        override val textSecondary = Color(0xFF4D564B)
        override val primary = Color(0xFF1F6F43)
        override val onPrimary = Color(0xFFFFFFFF)
        override val primaryContainer = Color(0xFFD4EDDC)
        override val onPrimaryContainer = Color(0xFF0B3D21)
        override val error = Color(0xFFB3261E)
        override val onError = Color(0xFFFFFFFF)
        override val success = Color(0xFF1E7B34)
        override val warning = Color(0xFF8A5300)
        override val info = Color(0xFF1F5FA8)
        override val pillarExecution = Color(0xFF2563EB)
        override val pillarDamageControl = Color(0xFFC2410C)
        override val pillarConsistency = Color(0xFF7C3AED)
        override val pillarHabits = Color(0xFF15803D)
        override val confidenceLow = Color(0xFFA04E05)
        override val confidenceMedium = Color(0xFF1D4ED8)
        override val confidenceHigh = Color(0xFF15803D)
    }

    object Dark : Palette {
        override val background = Color(0xFF121411)
        override val surface = Color(0xFF1B1E1A)
        override val surfaceVariant = Color(0xFF262A25)
        override val border = Color(0xFF3C4239)
        override val borderStrong = Color(0xFF8C9688)
        override val textPrimary = Color(0xFFE6EAE3)
        override val textSecondary = Color(0xFFB5BDB0)
        override val primary = Color(0xFF7BD49B)
        override val onPrimary = Color(0xFF003919)
        override val primaryContainer = Color(0xFF1C5235)
        override val onPrimaryContainer = Color(0xFFBDF0CE)
        override val error = Color(0xFFF2B8B5)
        override val onError = Color(0xFF601410)
        override val success = Color(0xFF7CD992)
        override val warning = Color(0xFFF5B65A)
        override val info = Color(0xFF9CC4FF)
        override val pillarExecution = Color(0xFF7AA7FF)
        override val pillarDamageControl = Color(0xFFFF9A62)
        override val pillarConsistency = Color(0xFFB79BFF)
        override val pillarHabits = Color(0xFF6FD68F)
        override val confidenceLow = Color(0xFFF5B65A)
        override val confidenceMedium = Color(0xFF9CC4FF)
        override val confidenceHigh = Color(0xFF7CD992)
    }

    object Spacing {
        val xxs = 2.dp
        val xs = 4.dp
        val sm = 8.dp
        val md = 12.dp
        val lg = 16.dp
        val xl = 24.dp
        val xxl = 32.dp
        val xxxl = 48.dp
    }

    object Radius {
        val sm = 4.dp
        val md = 8.dp
        val lg = 16.dp
        val full = 999.dp
    }

    /** In sp, so text scales with the user's font size. */
    object Typography {
        val display = TextStyle(fontSize = 34.sp, lineHeight = 41.sp, fontWeight = FontWeight.Bold)
        val title = TextStyle(fontSize = 28.sp, lineHeight = 34.sp, fontWeight = FontWeight.SemiBold)
        val headline = TextStyle(fontSize = 22.sp, lineHeight = 28.sp, fontWeight = FontWeight.SemiBold)
        val body = TextStyle(fontSize = 17.sp, lineHeight = 22.sp, fontWeight = FontWeight.Normal)
        val callout = TextStyle(fontSize = 16.sp, lineHeight = 21.sp, fontWeight = FontWeight.Normal)
        val label = TextStyle(fontSize = 15.sp, lineHeight = 20.sp, fontWeight = FontWeight.Medium)
        val caption = TextStyle(fontSize = 13.sp, lineHeight = 18.sp, fontWeight = FontWeight.Normal)
    }

    val touchTarget = 48.dp
}
