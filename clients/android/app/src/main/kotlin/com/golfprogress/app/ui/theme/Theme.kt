package com.golfprogress.app.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf

/**
 * The app's Material 3 theme, built only from the generated [DesignTokens] (#73, #77), so
 * it matches web and iOS. Light or dark follows the system setting.
 */
@Composable
fun GolfProgressTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    val palette = if (darkTheme) DesignTokens.Dark else DesignTokens.Light
    CompositionLocalProvider(LocalPalette provides palette) {
        MaterialTheme(
            colorScheme = colorScheme(palette, darkTheme),
            typography = typography,
            content = content,
        )
    }
}

/** The full token palette, for colours Material has no slot for (pillars, confidence). */
val LocalPalette = staticCompositionLocalOf<DesignTokens.Palette> { DesignTokens.Light }

private fun colorScheme(
    p: DesignTokens.Palette,
    dark: Boolean,
): ColorScheme {
    val base = if (dark) darkColorScheme() else lightColorScheme()
    return base.copy(
        primary = p.primary,
        onPrimary = p.onPrimary,
        primaryContainer = p.primaryContainer,
        onPrimaryContainer = p.onPrimaryContainer,
        error = p.error,
        onError = p.onError,
        background = p.background,
        onBackground = p.textPrimary,
        surface = p.surface,
        onSurface = p.textPrimary,
        surfaceVariant = p.surfaceVariant,
        onSurfaceVariant = p.textSecondary,
        surfaceContainer = p.surface,
        outline = p.borderStrong,
        outlineVariant = p.border,
    )
}

private val typography =
    Typography(
        displaySmall = DesignTokens.Typography.display,
        headlineMedium = DesignTokens.Typography.title,
        titleLarge = DesignTokens.Typography.headline,
        bodyLarge = DesignTokens.Typography.body,
        bodyMedium = DesignTokens.Typography.callout,
        labelLarge = DesignTokens.Typography.label,
        bodySmall = DesignTokens.Typography.caption,
    )
