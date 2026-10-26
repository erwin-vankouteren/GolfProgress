package com.golfprogress.app

import android.content.Context
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.app.auth.AuthFlow
import com.golfprogress.app.language.AppLocales
import com.golfprogress.app.language.LanguageSync
import com.golfprogress.app.navigation.MainScreen
import com.golfprogress.app.profile.OnboardingScreen
import com.golfprogress.app.session.ServerWakingHint
import com.golfprogress.app.session.SessionState
import com.golfprogress.app.session.SessionViewModel
import com.golfprogress.app.ui.theme.DesignTokens
import com.golfprogress.app.ui.theme.GolfProgressTheme
import com.golfprogress.app.version.UpdateRequiredScreen
import com.golfprogress.app.version.VersionState
import com.golfprogress.app.version.VersionViewModel
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    // Before Android 13 the app applies the chosen language itself; see AppLocales.
    override fun attachBaseContext(newBase: Context) {
        super.attachBaseContext(AppLocales.wrap(newBase))
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            GolfProgressTheme {
                Root()
            }
        }
    }
}

/**
 * Shows "Update required" instead of the app when this build is older than the API's
 * minimum (#66, #77). Otherwise: the main screen when signed in, Welcome when not (#71).
 * The version check never blocks start-up.
 */
@Composable
private fun Root(
    versionViewModel: VersionViewModel = hiltViewModel(),
    sessionViewModel: SessionViewModel = hiltViewModel(),
) {
    val version by versionViewModel.state.collectAsStateWithLifecycle()
    val session by sessionViewModel.state.collectAsStateWithLifecycle()
    // Onboarding ends on Log round (#113): the first round is the point of signing up.
    var openLogRound by rememberSaveable { mutableStateOf(false) }

    when {
        version == VersionState.UpdateRequired -> UpdateRequiredScreen()
        else ->
            when (session) {
                SessionState.Restoring -> RestoringScreen()
                SessionState.RestoreFailed -> RestoreFailedScreen(onRetry = sessionViewModel::retry)
                SessionState.SignedOut -> AuthFlow()
                is SessionState.SignedIn ->
                    if ((session as SessionState.SignedIn).newAccount) {
                        OnboardingScreen(
                            onFinished = {
                                openLogRound = true
                                sessionViewModel.onboarded()
                            },
                            onSkip = sessionViewModel::onboarded,
                        )
                    } else {
                        LanguageSync((session as SessionState.SignedIn).userId)
                        MainScreen(onSignOut = sessionViewModel::signOut, openLogRound = openLogRound)
                    }
            }
    }
}

@Composable
private fun RestoringScreen() {
    CenteredColumn {
        CircularProgressIndicator()
        Text(stringResource(R.string.session_restoring), style = MaterialTheme.typography.bodyLarge)
        ServerWakingHint(waiting = true)
    }
}

/** The API couldn't be reached at launch; the stored session is kept to try again. */
@Composable
fun RestoreFailedScreen(onRetry: () -> Unit) {
    CenteredColumn {
        Text(
            text = stringResource(R.string.auth_cant_connect),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Button(onClick = onRetry, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.try_again))
        }
    }
}

@Composable
private fun CenteredColumn(content: @Composable () -> Unit) {
    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .padding(DesignTokens.Spacing.xl),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        content()
    }
}
