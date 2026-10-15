package com.golfprogress.api

import com.golfprogress.api.generated.apis.AuthApi
import com.golfprogress.api.generated.apis.RoundsApi
import com.golfprogress.api.generated.apis.UsersApi
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.LoginRequest
import com.golfprogress.api.generated.models.UserResponse
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.time.OffsetDateTime
import java.util.concurrent.atomic.AtomicInteger

/**
 * Tests for the generated client and the hand-written pieces around it (#65), against a
 * local HTTP server so the real OkHttp interceptor and authenticator run.
 */
class GolfProgressApiTest {
    private lateinit var server: MockWebServer

    @BeforeEach
    fun start() {
        server = MockWebServer()
        server.start()
    }

    @AfterEach
    fun stop() = server.shutdown()

    private fun api(tokens: AccessTokenProvider = FakeTokens()) =
        GolfProgressApi(server.url("/").toString(), tokens)

    // Generated client, end to end

    @Test
    fun `getMe sends the token and decodes a typed user`() = runBlocking {
        server.enqueue(json(200, USER_JSON))

        val me = api().service<UsersApi>().getMe().unwrap()

        assertEquals("Erwin", me.displayName)
        assertEquals(UserResponse.Units.Metric, me.units)
        assertEquals(listOf("putting"), me.goalFocus)
        assertNull(me.handicapIndex)
        // .NET writes seven fractional digits; java.time keeps them.
        assertEquals(OffsetDateTime.parse("2026-05-01T09:30:00.1234567+00:00"), me.createdAt)

        val request = server.takeRequest()
        assertEquals("/me", request.path)
        assertEquals("Bearer token-1", request.getHeader("Authorization"))
    }

    @Test
    fun `paged lists send their query and expose the total`() = runBlocking {
        server.enqueue(json(200, "[]").addHeader("X-Total-Count", "25"))

        val page = api().service<RoundsApi>().listMyRounds(skip = 20, take = 10)

        assertEquals(emptyList<Any>(), page.unwrap())
        assertEquals(25, page.totalCount())
        assertEquals("/me/rounds?skip=20&take=10", server.takeRequest().path)
    }

    @Test
    fun `request bodies are typed JSON`() = runBlocking {
        server.enqueue(json(201, ROUND_JSON))

        api().service<RoundsApi>().createRound(
            CreateRoundRequest(playedAt = "2026-05-01", totalStrokes = 88, holes = 18),
        ).unwrap()

        val body = server.takeRequest().body.readUtf8()
        assertEquals(true, body.contains("\"playedAt\":\"2026-05-01\""), body)
        assertEquals(true, body.contains("\"totalStrokes\":88"), body)
    }

    // Errors

    @Test
    fun `an error response throws the ApiProblem`() = runBlocking {
        server.enqueue(problem(404, """{"title":"Not found","status":404,"detail":"User not found.","traceId":"00-abc"}"""))

        val problem = assertThrows(ApiProblem::class.java) {
            runBlocking { api().service<UsersApi>().getMe().unwrap() }
        }

        assertEquals(404, problem.status)
        assertEquals("User not found.", problem.detail)
        assertEquals("00-abc", problem.traceId)
        assertEquals("User not found.", problem.message)
    }

    @Test
    fun `validation messages are keyed by field, case-insensitively`() {
        val problem = ApiProblem.from(
            400,
            """{"status":400,"errors":{"PlayedAt":["PlayedAt must be an ISO date, yyyy-MM-dd."]}}""",
        )

        assertEquals(listOf("PlayedAt must be an ISO date, yyyy-MM-dd."), problem.messagesFor("playedAt"))
        assertEquals(emptyList<String>(), problem.messagesFor("holes"))
    }

    @Test
    fun `a body that isn't ProblemDetails still gives the status`() {
        val problem = ApiProblem.from(502, "Bad gateway")

        assertEquals(502, problem.status)
        assertEquals("Request failed with status 502", problem.message)
    }

    // Access token refresh

    @Test
    fun `a 401 refreshes once and retries with the new token`() = runBlocking {
        val tokens = FakeTokens()
        server.enqueue(problem(401, """{"status":401}"""))
        server.enqueue(json(200, USER_JSON))

        val me = api(tokens).service<UsersApi>().getMe().unwrap()

        assertEquals("Erwin", me.displayName)
        assertEquals(1, tokens.refreshes.get())
        assertEquals("Bearer token-1", server.takeRequest().getHeader("Authorization"))
        assertEquals("Bearer token-2", server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun `when refreshing fails the original 401 is returned`() = runBlocking {
        val tokens = FakeTokens(refreshed = null)
        server.enqueue(problem(401, """{"status":401,"title":"Unauthorized"}"""))

        val response = api(tokens).service<UsersApi>().getMe()

        assertEquals(401, response.code())
        assertEquals(1, server.requestCount)
    }

    @Test
    fun `a refused refreshed token is not retried again`() = runBlocking {
        server.enqueue(problem(401, "{}"))
        server.enqueue(problem(401, "{}"))
        server.enqueue(json(200, USER_JSON))

        val response = api().service<UsersApi>().getMe()

        assertEquals(401, response.code())
        assertEquals(2, server.requestCount)
    }

    @Test
    fun `concurrent 401s share a single refresh`() = runBlocking {
        val tokens = FakeTokens()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse =
                if (request.getHeader("Authorization") == "Bearer token-2") json(200, USER_JSON) else problem(401, "{}")
        }
        val users = api(tokens).service<UsersApi>()

        val results = (1..5).map { async(Dispatchers.IO) { users.getMe().code() } }.awaitAll()

        assertEquals(listOf(200, 200, 200, 200, 200), results)
        assertEquals(1, tokens.refreshes.get())
    }

    /** A wrong password answers 401 too; that must not be mistaken for an expired token. */
    @Test
    fun `sign-in requests carry no token and never refresh`() = runBlocking {
        val tokens = FakeTokens()
        server.enqueue(problem(401, """{"status":401}"""))

        val response = api(tokens).service<AuthApi>().login(loginRequest = LoginRequest(email = "a@example.test", password = "wrong"))

        assertEquals(401, response.code())
        assertNull(server.takeRequest().getHeader("Authorization"))
        assertEquals(0, tokens.refreshes.get())
    }

    @Test
    fun `no token is sent when signed out`() = runBlocking {
        server.enqueue(problem(401, "{}"))

        api(FakeTokens(current = null)).service<UsersApi>().getMe()

        assertNull(server.takeRequest().getHeader("Authorization"))
    }

    // Fixtures

    private class FakeTokens(current: String? = "token-1", private val refreshed: String? = "token-2") : AccessTokenProvider {
        @Volatile private var token = current
        val refreshes = AtomicInteger()

        override fun accessToken() = token

        override fun refreshAccessToken(): String? {
            refreshes.incrementAndGet()
            Thread.sleep(50) // long enough for concurrent requests to pile up behind it
            token = refreshed
            return refreshed
        }
    }

    private companion object {
        fun json(code: Int, body: String) = MockResponse().setResponseCode(code)
            .setHeader("Content-Type", "application/json; charset=utf-8").setBody(body)

        fun problem(code: Int, body: String) = MockResponse().setResponseCode(code)
            .setHeader("Content-Type", "application/problem+json; charset=utf-8").setBody(body)

        const val USER_JSON = """
            {
              "userId": "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
              "email": "erwin@example.test",
              "displayName": "Erwin",
              "timezone": null,
              "units": "Metric",
              "trackingMode": "Minimal",
              "handicapIndex": null,
              "typical18Score": null,
              "goalFocus": ["putting"],
              "createdAt": "2026-05-01T09:30:00.1234567+00:00",
              "updatedAt": "2026-05-01T09:30:00+00:00"
            }
        """

        const val ROUND_JSON = """
            {
              "roundId": "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a",
              "userId": "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
              "playedAt": "2026-05-01",
              "holes": 18,
              "courseName": null,
              "teeName": null,
              "totalStrokes": 88,
              "totalPutts": null,
              "penalties": null,
              "fairwaysHit": null,
              "gir": null,
              "threePutts": null,
              "blowUpHoles": null,
              "source": "Manual",
              "notes": null,
              "createdAt": "2026-05-01T09:30:00+00:00",
              "updatedAt": "2026-05-01T09:30:00+00:00"
            }
        """
    }
}
