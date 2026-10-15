package com.golfprogress.app.sections

import androidx.annotation.StringRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.golfprogress.app.R
import com.golfprogress.app.ui.theme.DesignTokens

/**
 * A section's content in the app shell (#77): the empty state from the wireframes. The
 * features replace these in M2.
 */
@Composable
fun SectionContent(
    @StringRes emptyMessage: Int,
    onLogRound: (() -> Unit)? = null,
) {
    Surface(
        modifier = Modifier.fillMaxWidth().padding(DesignTokens.Spacing.lg),
        shape = RoundedCornerShape(DesignTokens.Radius.lg),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        color = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier.padding(DesignTokens.Spacing.xl),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md),
        ) {
            Text(
                text = stringResource(emptyMessage),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            if (onLogRound != null) {
                Button(onClick = onLogRound, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                    Text(stringResource(R.string.log_round))
                }
            }
        }
    }
}

/** Profile (#77); sign out (#71). Preferences and account settings arrive in M2. */
@Composable
fun ProfileContent(onSignOut: () -> Unit) {
    Column {
        SectionContent(R.string.profile_intro)
        OutlinedButton(
            onClick = onSignOut,
            modifier = Modifier.padding(horizontal = DesignTokens.Spacing.lg).heightIn(min = DesignTokens.touchTarget),
        ) {
            Text(stringResource(R.string.auth_sign_out))
        }
    }
}
