package com.golfprogress.api

import com.golfprogress.api.generated.apis.PracticeSessionsApi
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.Arguments
import org.junit.jupiter.params.provider.MethodSource
import java.io.File
import java.util.UUID

/** Regression guard for #134: open enums never break this build when the API adds a value. */
class KnownValuesTest {

    @Test
    fun `a value added to the API after this build is read without failing`() = runBlocking {
        val server = MockWebServer().apply { start() }
        try {
            server.enqueue(
                MockResponse().setResponseCode(200).setHeader("Content-Type", "application/json").setBody(
                    """
                    {
                      "practiceSessionId": "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a",
                      "userId": "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
                      "practicedAt": "2026-05-01",
                      "durationMinutes": 45,
                      "sessionType": "Fitness",
                      "venueName": null,
                      "tags": [],
                      "primaryGoal": null,
                      "notes": null,
                      "createdAt": "2026-05-01T09:30:00+00:00"
                    }
                    """,
                ),
            )
            val api = GolfProgressApi(server.url("/").toString(), NoTokens)

            val session = api.service<PracticeSessionsApi>()
                .getPracticeSession(UUID.fromString("6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a"))
                .unwrap()

            assertEquals("Fitness", session.sessionType)
            assertEquals(SessionType.Unknown, session.knownSessionType)
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun `known values map to their case`() {
        assertEquals(SessionType.Putting, SessionType.of("Putting"))
        assertEquals(MicroWinCategory.Habit, MicroWinCategory.of("Habit"))
        assertEquals(RoundSource.Import, RoundSource.of("Import"))
        assertEquals(RoundSource.Unknown, RoundSource.of(null))
    }

    /** So the build knows every value the API already returns. */
    @ParameterizedTest(name = "{0}.{1}")
    @MethodSource("openEnums")
    fun `lists the same known values as the spec`(schema: String, property: String, known: List<String>) {
        val schemas = spec["components"]!!.jsonObject["schemas"]!!.jsonObject
        val listed = (schemas[schema]!!.jsonObject["properties"]!!.jsonObject[property]!!.jsonObject["x-known-values"] as JsonArray)
            .map { it.jsonPrimitive.content }

        assertEquals(known, listed)
    }

    companion object {
        /** Gradle runs tests from the module folder: clients/android/api-client. */
        private val spec: JsonObject by lazy {
            Json.parseToJsonElement(File("../../../api/openapi.json").readText()).jsonObject
        }

        private fun <E : Enum<E>> apiValues(entries: List<E>, apiValue: (E) -> String?) = entries.mapNotNull(apiValue)

        @JvmStatic
        fun openEnums(): List<Arguments> = listOf(
            Arguments.of("PracticeSessionResponse", "sessionType", apiValues(SessionType.entries) { it.apiValue }),
            Arguments.of("MicroWinResponse", "category", apiValues(MicroWinCategory.entries) { it.apiValue }),
            Arguments.of("RoundResponse", "source", apiValues(RoundSource.entries) { it.apiValue }),
        )
    }

    private object NoTokens : AccessTokenProvider {
        override fun accessToken(): String? = null
        override fun refreshAccessToken(): String? = null
    }
}
