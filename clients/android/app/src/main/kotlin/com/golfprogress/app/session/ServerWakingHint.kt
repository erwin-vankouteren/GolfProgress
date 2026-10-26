package com.golfprogress.app.session

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import com.golfprogress.app.R
import kotlinx.coroutines.delay

/** How long a wait lasts before the screen explains it. */
const val SLOW_AFTER_MILLIS = 5_000L

/**
 * Once `waiting` has lasted [SLOW_AFTER_MILLIS], says the server is waking up, so a
 * sign-in that waits on staging's database resuming doesn't look stuck.
 */
@Composable
fun ServerWakingHint(
    waiting: Boolean,
    modifier: Modifier = Modifier,
) {
    var slow by remember { mutableStateOf(false) }
    LaunchedEffect(waiting) {
        slow = false
        if (waiting) {
            delay(SLOW_AFTER_MILLIS)
            slow = true
        }
    }
    if (waiting && slow) {
        Text(
            text = stringResource(R.string.session_server_waking),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = modifier.semantics { liveRegion = LiveRegionMode.Polite },
        )
    }
}
