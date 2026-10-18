package com.golfprogress.app.language

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.platform.LocalContext
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import com.golfprogress.app.data.UserRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import javax.inject.Inject

@HiltViewModel
class LanguageSyncViewModel
    @Inject
    constructor(
        private val user: UserRepository,
    ) : ViewModel() {
        /** The profile's language; null when it follows the device. */
        suspend fun profileChoice(): AppLanguage? = AppLanguage.fromApi(user.me().language)
    }

/**
 * Takes over the language chosen in the profile, e.g. on the web, the first time an
 * account is signed in on this device (see [AppLocales.syncedFor]). If the profile
 * can't be read now, it's tried again at the next launch.
 */
@Composable
fun LanguageSync(
    userId: String,
    viewModel: LanguageSyncViewModel = hiltViewModel(),
) {
    val context = LocalContext.current
    LaunchedEffect(userId) {
        if (AppLocales.syncedFor(context) == userId) return@LaunchedEffect
        val choice =
            try {
                viewModel.profileChoice()
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                return@LaunchedEffect
            }
        // Marked first: applying a new language recreates the activity.
        AppLocales.markSynced(context, userId)
        AppLocales.apply(context, choice)
    }
}
