package com.golfprogress.api

import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route

/**
 * Supplies access tokens to the API client (#65).
 *
 * The session layer (#71) implements this: it keeps the access token in memory and the
 * refresh token encrypted with an Android Keystore key. Both methods are called on
 * OkHttp's background threads and may block.
 */
interface AccessTokenProvider {
    /** The current access token, or null when signed out. */
    fun accessToken(): String?

    /**
     * Exchanges the refresh token for a new access token after a 401. Returns null when
     * the session can't be refreshed and the user must sign in again.
     */
    fun refreshAccessToken(): String?
}

/**
 * Paths reached before signing in. They never carry a token, and a 401 from them (such
 * as a wrong password on login) must not trigger a refresh.
 */
internal val anonymousPaths = listOf("/auth/register", "/auth/login", "/auth/refresh", "/auth/logout", "/client-config")

internal fun Request.isAnonymous(): Boolean = anonymousPaths.any { url.encodedPath.endsWith(it) }

private fun Request.withToken(token: String): Request =
    newBuilder().header("Authorization", "Bearer $token").build()

/** Sends the access token on authenticated requests (#65). */
class AuthInterceptor(private val tokens: AccessTokenProvider) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.isAnonymous()) return chain.proceed(request)

        val token = tokens.accessToken() ?: return chain.proceed(request)
        return chain.proceed(request.withToken(token))
    }
}

/**
 * On a 401, refreshes the access token once and retries the request (#65).
 *
 * Concurrent requests that all hit a 401 share a single refresh: each refresh rotates
 * the refresh token, so a second, parallel refresh would invalidate the first. A
 * request whose token was already replaced by another thread's refresh just retries
 * with the new token.
 */
class TokenAuthenticator(private val tokens: AccessTokenProvider) : Authenticator {
    private val lock = Any()

    override fun authenticate(route: Route?, response: Response): Request? {
        val request = response.request
        // Only once per request: a 401 on the retry means the refreshed token was refused too.
        if (request.isAnonymous() || response.priorResponse != null) return null

        val failedToken = request.header("Authorization")?.removePrefix("Bearer ") ?: return null

        val token = synchronized(lock) {
            val current = tokens.accessToken()
            if (current != null && current != failedToken) current else tokens.refreshAccessToken()
        } ?: return null

        return request.withToken(token)
    }
}
