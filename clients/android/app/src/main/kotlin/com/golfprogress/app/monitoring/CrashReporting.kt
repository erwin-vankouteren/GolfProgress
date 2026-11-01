package com.golfprogress.app.monitoring

import android.content.Context
import com.golfprogress.api.ApiProblem
import io.sentry.Sentry
import io.sentry.SentryEvent
import io.sentry.SentryLevel
import io.sentry.android.core.SentryAndroid
import io.sentry.protocol.Message
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Crash and error reporting (#119), through Sentry's EU region: crashes, ANRs, and 5xx
 * responses with the API's `traceId`, so a report can be found in the API's logs. Off
 * unless the build has a DSN (the release workflow passes -PsentryDsn), so debug builds
 * and tests send nothing.
 *
 * No personal data leaves the phone: no user or install id, no IP address, no screenshots
 * or view hierarchy, and no sessions. What's sent: docs/legal/store-declarations.md →
 * "Crash reports".
 */
object CrashReporting {
    fun start(
        context: Context,
        dsn: String,
    ) {
        if (dsn.isEmpty()) return
        SentryAndroid.init(context) { options ->
            options.dsn = dsn
            options.environment = "production"
            options.isSendDefaultPii = false
            options.isAttachScreenshot = false
            options.isAttachViewHierarchy = false
            options.isEnableAutoSessionTracking = false
            options.setBeforeSend { event, _ -> scrub(event) }
        }
    }

    /** Leaves out the install id Sentry adds as the user, and tags an escaped [ApiProblem] with its traceId. */
    fun scrub(event: SentryEvent): SentryEvent {
        event.user = null
        (event.throwable as? ApiProblem)?.let { problem ->
            event.setTag("api.status", problem.status.toString())
            problem.traceId?.let { event.setTag("api.traceId", it) }
        }
        return event
    }

    /** Reports a 5xx from the API, grouped by endpoint rather than by each round's id. */
    fun reportServerError(error: ServerError) {
        Sentry.captureEvent(serverErrorEvent(error))
    }

    fun serverErrorEvent(error: ServerError): SentryEvent {
        val endpoint = "${error.method} ${routeOf(error.path)}"
        return SentryEvent().apply {
            message = Message().apply { message = "API ${error.status}: $endpoint" }
            level = SentryLevel.ERROR
            fingerprints = listOf("api", endpoint, error.status.toString())
            setTag("api.status", error.status.toString())
            error.traceId?.let { setTag("api.traceId", it) }
        }
    }

    private val guid = Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOption.IGNORE_CASE)

    /** /me/rounds/3f2b8c1e-… becomes /me/rounds/{id}. */
    fun routeOf(path: String): String = path.replace(guid, "{id}")
}

/** A 5xx response: the request, and the `traceId` that finds it in the API's logs. */
data class ServerError(
    val method: String,
    /** The path without its query, e.g. /me/rounds/3f2b…. */
    val path: String,
    val status: Int,
    val traceId: String?,
)

/** Hands every 5xx response to [report], with the traceId from its ProblemDetails body. */
class ServerErrorInterceptor(
    private val report: (ServerError) -> Unit = CrashReporting::reportServerError,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val response = chain.proceed(request)
        if (response.code >= 500) {
            val body = runCatching { response.peekBody(MAX_BODY).string() }.getOrNull()
            report(ServerError(request.method, request.url.encodedPath, response.code, ApiProblem.from(response.code, body).traceId))
        }
        return response
    }

    private companion object {
        const val MAX_BODY = 64L * 1024
    }
}
