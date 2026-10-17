package com.golfprogress.app.session

import com.golfprogress.api.AccessTokenProvider
import com.golfprogress.api.ApiProblem
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.runBlocking

sealed interface SessionState {
    /** Checking the stored refresh token with the API, at launch. */
    data object Restoring : SessionState

    /** The API couldn't be reached at launch. The stored token is kept, to try again. */
    data object RestoreFailed : SessionState

    data object SignedOut : SessionState

    data class SignedIn(
        val userId: String,
        /** Just created: onboarding comes first (#113). */
        val newAccount: Boolean = false,
    ) : SessionState
}

/**
 * The sign-in state the UI follows (#71), on top of [TokenRefresher].
 *
 * It's also the API client's [AccessTokenProvider]: when the refresh after a 401 finds
 * the session has ended, the state becomes [SessionState.SignedOut] and the app returns
 * to Welcome.
 */
class Session(
    private val tokens: TokenRefresher,
) : AccessTokenProvider {
    private val mutableState = MutableStateFlow<SessionState>(SessionState.Restoring)
    val state: StateFlow<SessionState> = mutableState.asStateFlow()

    /** At launch, and from "Try again" after [SessionState.RestoreFailed]. */
    suspend fun restore() {
        mutableState.value = SessionState.Restoring
        mutableState.value =
            try {
                tokens.refresh()?.let { SessionState.SignedIn(it.userId.toString()) } ?: SessionState.SignedOut
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                SessionState.RestoreFailed
            }
    }

    suspend fun signIn(
        email: String,
        password: String,
    ) {
        mutableState.value = SessionState.SignedIn(tokens.signIn(email, password))
    }

    suspend fun register(
        email: String,
        displayName: String,
        password: String,
    ) {
        mutableState.value = SessionState.SignedIn(tokens.register(email, displayName, password), newAccount = true)
    }

    /** Onboarding is finished or skipped: on to the app. */
    fun onboarded() {
        mutableState.update { if (it is SessionState.SignedIn) it.copy(newAccount = false) else it }
    }

    /** Emails a password reset link (#86); the link opens the web app to choose a new password. */
    suspend fun requestPasswordReset(email: String) = tokens.requestPasswordReset(email)

    suspend fun signOut() {
        tokens.signOut()
        mutableState.value = SessionState.SignedOut
    }

    // AccessTokenProvider: called on OkHttp's threads, where blocking is allowed.

    override fun accessToken(): String? = tokens.accessToken()

    override fun refreshAccessToken(): String? {
        val refreshed =
            try {
                runBlocking { tokens.refresh() }
            } catch (_: ApiProblem) {
                // Throttled or failing: the original 401 stands, but the session may be fine.
                return null
            }
        if (refreshed == null) {
            mutableState.update { if (it is SessionState.SignedIn) SessionState.SignedOut else it }
        }
        return refreshed?.token
    }
}
