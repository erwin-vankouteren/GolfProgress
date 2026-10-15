package com.golfprogress.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.app.navigation.MainScreen
import com.golfprogress.app.ui.theme.GolfProgressTheme
import com.golfprogress.app.version.UpdateRequiredScreen
import com.golfprogress.app.version.VersionState
import com.golfprogress.app.version.VersionViewModel
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
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
 * minimum (#66, #77); otherwise the main screen. The check never blocks start-up.
 */
@Composable
private fun Root(viewModel: VersionViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    if (state == VersionState.UpdateRequired) UpdateRequiredScreen() else MainScreen()
}
