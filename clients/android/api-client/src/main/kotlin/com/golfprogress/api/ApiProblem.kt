package com.golfprogress.api

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import retrofit2.Response

/**
 * An error response from the API, from its ProblemDetails body (#65).
 *
 * Every API error is `application/problem+json`. [unwrap] throws this for any non-2xx
 * response, so a call either returns its body or throws: view models never inspect
 * status codes themselves.
 */
class ApiProblem(
    /** HTTP status code, e.g. 400, 404, 409, 429, 500. */
    val status: Int,
    val title: String? = null,
    val detail: String? = null,
    /** Matches the API's log entry for this request. Show it in debug builds only. */
    val traceId: String? = null,
    /** For 400s: validation messages keyed by field, to show next to that field. */
    val errors: Map<String, List<String>> = emptyMap(),
    /** For 429s: seconds to wait before trying again, from the Retry-After header. */
    val retryAfterSeconds: Int? = null,
) : RuntimeException(detail ?: title ?: "Request failed with status $status") {

    /**
     * The validation messages for one field, matched case-insensitively: the API keys
     * body fields as `PlayedAt` and query parameters as `from`.
     */
    fun messagesFor(field: String): List<String> =
        errors.entries.firstOrNull { it.key.equals(field, ignoreCase = true) }?.value.orEmpty()

    companion object {
        private val json = Json { ignoreUnknownKeys = true }

        /**
         * Builds the problem from a response body; any body that isn't ProblemDetails still
         * gives a status. [retryAfterSeconds] comes from the response's Retry-After header.
         */
        fun from(
            status: Int,
            body: String?,
            retryAfterSeconds: Int? = null,
        ): ApiProblem {
            val problem = body?.let { runCatching { json.parseToJsonElement(it) as? JsonObject }.getOrNull() }
                ?: return ApiProblem(status, retryAfterSeconds = retryAfterSeconds)

            fun string(key: String) = (problem[key] as? JsonPrimitive)?.contentOrNull

            val errors = (problem["errors"] as? JsonObject)?.mapValues { (_, messages) ->
                (messages as? JsonArray)?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull }.orEmpty()
            }.orEmpty()

            return ApiProblem(
                status = (problem["status"] as? JsonPrimitive)?.intOrNull ?: status,
                title = string("title"),
                detail = string("detail"),
                traceId = string("traceId"),
                errors = errors,
                retryAfterSeconds = retryAfterSeconds,
            )
        }
    }
}

/**
 * The body of a successful response, or a thrown [ApiProblem] for an error response.
 * For a body-less success (204) the result is [Unit].
 */
@Suppress("UNCHECKED_CAST")
fun <T> Response<T>.unwrap(): T {
    if (!isSuccessful) {
        // The API sends delta-seconds, never an HTTP date.
        throw ApiProblem.from(code(), errorBody()?.string(), headers()["Retry-After"]?.trim()?.toIntOrNull())
    }
    return (body() ?: Unit) as T
}

/** The total item count of a paged list response (`X-Total-Count`). */
fun Response<*>.totalCount(): Int? = headers()["X-Total-Count"]?.toIntOrNull()
