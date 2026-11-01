package com.golfprogress.app.monitoring

import com.golfprogress.api.ApiProblem
import io.sentry.SentryEvent
import io.sentry.protocol.User
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Crash reports (#119): 5xx responses with their traceId, and nothing that identifies the user. */
class CrashReportingTest {
    private val server = MockWebServer()

    @After
    fun stop() = server.shutdown()

    @Test
    fun aServerError_isReportedWithItsTraceId_andTheCallerStillGetsTheBody() {
        val body = """{"title":"Server error","status":500,"traceId":"00-abc"}"""
        server.enqueue(MockResponse().setResponseCode(500).setBody(body))
        val reported = mutableListOf<ServerError>()

        val received = call("/me/rounds?skip=0", reported)

        assertEquals(listOf(ServerError("GET", "/me/rounds", 500, "00-abc")), reported)
        assertEquals(body, received)
    }

    @Test
    fun aClientError_isNotReported() {
        server.enqueue(MockResponse().setResponseCode(404).setBody("""{"status":404}"""))
        val reported = mutableListOf<ServerError>()

        call("/me", reported)

        assertTrue(reported.isEmpty())
    }

    @Test
    fun serverErrors_areGroupedByEndpoint_notById() {
        val event = CrashReporting.serverErrorEvent(ServerError("PUT", "/rounds/3F2B8C1E-5D4A-4B6F-9E2D-1A7C8B9D0E1F", 503, "00-abc"))

        assertEquals("API 503: PUT /rounds/{id}", event.message?.message)
        assertEquals(listOf("api", "PUT /rounds/{id}", "503"), event.fingerprints)
        assertEquals("00-abc", event.getTag("api.traceId"))
    }

    @Test
    fun aReport_leavesOutTheUser_andTagsAnApiProblemWithItsTraceId() {
        val event = SentryEvent(ApiProblem(500, traceId = "00-abc")).apply { user = User().apply { id = "install-id" } }

        val scrubbed = CrashReporting.scrub(event)

        assertNull(scrubbed.user)
        assertEquals("500", scrubbed.getTag("api.status"))
        assertEquals("00-abc", scrubbed.getTag("api.traceId"))
    }

    private fun call(
        path: String,
        reported: MutableList<ServerError>,
    ): String =
        OkHttpClient
            .Builder()
            .addInterceptor(ServerErrorInterceptor { reported += it })
            .build()
            .newCall(Request.Builder().url(server.url(path)).build())
            .execute()
            .use { it.body.string() }
}
