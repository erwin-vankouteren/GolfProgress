package com.golfprogress.app.ui.common

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import androidx.annotation.StringRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.golfprogress.app.R
import com.golfprogress.app.ui.theme.DesignTokens

// The states every screen handles (docs/clients/ux-design.md → "States every screen
// handles"): skeletons while loading, plain-language errors with Try again, empty states
// with the action that fills them, and the offline banner.

/** Placeholders in the shape of what's coming. TalkBack hears "Loading…" once. */
@Composable
fun Skeleton(
    blocks: Int,
    modifier: Modifier = Modifier,
    blockHeight: Int = 96,
) {
    val loading = stringResource(R.string.loading)
    Column(
        modifier = modifier.fillMaxWidth().clearAndSetSemantics { contentDescription = loading },
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        repeat(blocks) {
            Box(
                Modifier
                    .fillMaxWidth()
                    .height(blockHeight.dp)
                    .background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(DesignTokens.Radius.lg)),
            )
        }
    }
}

/** A failed load: what couldn't be loaded, and Try again. */
@Composable
fun LoadError(
    @StringRes message: Int,
    onRetry: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md),
    ) {
        Text(
            text = stringResource(message),
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
        )
        OutlinedButton(onClick = onRetry, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.try_again))
        }
    }
}

/** Nothing here yet: why, and the action that fills it. */
@Composable
fun EmptyState(
    @StringRes message: Int,
    modifier: Modifier = Modifier,
    @StringRes action: Int? = null,
    onAction: () -> Unit = {},
) {
    OutlinedCard(modifier) {
        Text(
            text = stringResource(message),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        if (action != null) {
            Button(onClick = onAction, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                Text(stringResource(action))
            }
        }
    }
}

/** A card with a heading, as the screens' sections. */
@Composable
fun SectionCard(
    @StringRes title: Int,
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit,
) {
    OutlinedCard(modifier) {
        Text(
            text = stringResource(title),
            style = MaterialTheme.typography.titleLarge,
            modifier = Modifier.semantics { heading() },
        )
        content()
    }
}

@Composable
fun OutlinedCard(
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit,
) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(DesignTokens.Radius.lg),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        color = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier.padding(DesignTokens.Spacing.xl),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md),
            content = content,
        )
    }
}

/** "You're offline", across the top while there's no connection (v1 is online-only). */
@Composable
fun OfflineBanner(online: Boolean = rememberOnline()) {
    if (online) return
    Text(
        text = stringResource(R.string.offline),
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onPrimaryContainer,
        modifier =
            Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.primaryContainer)
                .padding(horizontal = DesignTokens.Spacing.lg, vertical = DesignTokens.Spacing.sm)
                .semantics { liveRegion = LiveRegionMode.Polite },
    )
}

/** Whether the device has an internet connection; recomposes when that changes. */
@Composable
fun rememberOnline(): Boolean {
    val context = LocalContext.current
    val manager = remember(context) { context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager }
    var online by remember { mutableStateOf(manager.isOnline()) }

    DisposableEffect(manager) {
        val callback =
            object : ConnectivityManager.NetworkCallback() {
                override fun onAvailable(network: Network) {
                    online = true
                }

                override fun onLost(network: Network) {
                    online = manager.isOnline()
                }
            }
        manager.registerDefaultNetworkCallback(callback)
        onDispose { manager.unregisterNetworkCallback(callback) }
    }
    return online
}

/** A network with internet, or one whose capabilities aren't known: offline only when sure. */
private fun ConnectivityManager.isOnline(): Boolean {
    val network = activeNetwork ?: return false
    return getNetworkCapabilities(network)?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) ?: true
}
