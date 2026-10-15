package com.golfprogress.app.session

import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.apis.AuthApi
import com.golfprogress.api.generated.models.AuthResponse
import com.golfprogress.api.generated.models.LoginRequest
import com.golfprogress.api.generated.models.RefreshRequest
import com.golfprogress.api.generated.models.RegisterRequest
import com.golfprogress.api.unwrap
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Deferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.async

/**
 * Holds the tokens and calls the API's auth endpoints (#71).
 *
 * - The **access token** lives only in memory.
 * - The **refresh token** lives encrypted in [RefreshTokenStore] and is read only to
 *   send it to the API. The API rotates it on every refresh; the new one replaces it.
 * - Concurrent callers share **one refresh**: the first starts it and the rest await the
 *   same result. Refreshing twice would rotate the token under the first.
 *
 * [auth] must be a client without a token provider: the auth calls are anonymous.
 */
class TokenRefresher(
    private val auth: AuthApi,
    private val store: RefreshTokenStore,
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
) {
    @Volatile private var token: String? = null
    private val lock = Any()
    private var inFlight: Deferred<AuthResponse?>? = null

    fun accessToken(): String? = token

    /**
     * New tokens, or null when the session has ended (nothing stored, or the API rejected
     * the refresh token). Throws when the API can't be reached or fails: the session may
     * still be valid, so nothing is forgotten.
     */
    suspend fun refresh(): AuthResponse? {
        val refresh =
            synchronized(lock) {
                inFlight ?: scope.async { performRefresh() }.also { started ->
                    inFlight = started
                    started.invokeOnCompletion { synchronized(lock) { if (inFlight === started) inFlight = null } }
                }
            }
        return refresh.await()
    }

    /** Returns the user's ID. Throws an [ApiProblem] for 400, 401 (wrong password) or 429. */
    suspend fun signIn(
        email: String,
        password: String,
    ): String = accept(auth.login(loginRequest = LoginRequest(email = email, password = password)).unwrap())

    /** Returns the user's ID. Throws an [ApiProblem] for 400, 409 (email taken) or 429. */
    suspend fun register(
        email: String,
        displayName: String,
        password: String,
    ): String =
        accept(
            auth
                .register(registerRequest = RegisterRequest(email = email, displayName = displayName, password = password))
                .unwrap(),
        )

    /**
     * Forgets the session here first, then revokes the refresh token on the API. If the
     * API can't be reached, the token simply expires there.
     */
    suspend fun signOut() {
        val stored = store.load()
        token = null
        store.clear()

        if (stored != null) {
            try {
                auth.logout(refreshRequest = RefreshRequest(refreshToken = stored))
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                // Offline: nothing more to do.
            }
        }
    }

    private suspend fun performRefresh(): AuthResponse? {
        val stored = store.load()
        if (stored == null) {
            token = null
            return null
        }

        val response =
            try {
                auth.refreshToken(refreshRequest = RefreshRequest(refreshToken = stored)).unwrap()
            } catch (problem: ApiProblem) {
                // 400/401: expired or revoked, so the user has to sign in again. Anything
                // else (429, 5xx) says nothing about the token.
                if (problem.status != 400 && problem.status != 401) throw problem
                token = null
                store.clear()
                return null
            }
        accept(response)
        return response
    }

    private suspend fun accept(response: AuthResponse): String {
        response.refreshToken?.let { store.save(it) }
        token = response.token
        return response.userId.toString()
    }
}
