package com.golfprogress.app.version

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.golfprogress.app.R
import com.golfprogress.app.ui.theme.DesignTokens
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Named

/**
 * Compares two major.minor.patch versions numerically: negative when [a] is older, zero
 * when equal, positive when newer. "1.10.0" is newer than "1.9.0". Unparseable parts
 * count as 0, so a malformed version never blocks the app.
 */
fun compareVersions(
    a: String,
    b: String,
): Int {
    val x = a.split(".").map { it.toIntOrNull() ?: 0 }
    val y = b.split(".").map { it.toIntOrNull() ?: 0 }
    for (i in 0 until maxOf(x.size, y.size)) {
        val diff = x.getOrElse(i) { 0 } - y.getOrElse(i) { 0 }
        if (diff != 0) return diff
    }
    return 0
}

/** The API's minimum supported Android version (GET /client-config, #66). */
fun interface MinimumVersionSource {
    suspend fun minimumVersion(): String
}

enum class VersionState { Checking, Supported, UpdateRequired }

/**
 * Checks this build against the API's minimum (#77). It never blocks start-up: the app
 * shows while the check runs, and if it fails (offline, API down) the app carries on.
 */
@HiltViewModel
class VersionViewModel
    @Inject
    constructor(
        private val source: MinimumVersionSource,
        @Named("appVersion") private val appVersion: String,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(VersionState.Checking)
        val state: StateFlow<VersionState> = mutableState.asStateFlow()

        init {
            viewModelScope.launch { mutableState.value = check() }
        }

        private suspend fun check(): VersionState =
            try {
                if (compareVersions(appVersion, source.minimumVersion()) < 0) {
                    VersionState.UpdateRequired
                } else {
                    VersionState.Supported
                }
            } catch (_: Exception) {
                VersionState.Supported
            }
    }

@Composable
fun UpdateRequiredScreen() {
    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .padding(DesignTokens.Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            text = stringResource(R.string.update_required_title),
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.semantics { heading() },
        )
        Text(
            text = stringResource(R.string.update_required_body),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(top = DesignTokens.Spacing.lg),
        )
    }
}
