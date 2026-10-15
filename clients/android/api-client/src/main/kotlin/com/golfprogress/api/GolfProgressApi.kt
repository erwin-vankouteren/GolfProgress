package com.golfprogress.api

import com.golfprogress.api.generated.infrastructure.Serializer
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * Entry point for the app (#65): Retrofit with the generated API interfaces, the
 * serializers the generated models need, and access-token handling.
 *
 * ```kotlin
 * val api = GolfProgressApi(baseUrl, tokens = session)
 * val me = api.service<UsersApi>().getMe().unwrap()
 * val page = api.service<RoundsApi>().listMyRounds(skip = 0, take = 20)
 * val rounds = page.unwrap(); val total = page.totalCount()
 * ```
 *
 * In the app, Hilt provides one instance (#77). Calls return their body or throw: an
 * [ApiProblem] for API errors, an IOException for network failures.
 */
class GolfProgressApi(
    baseUrl: String,
    tokens: AccessTokenProvider,
    okHttpClient: OkHttpClient = OkHttpClient(),
) {
    val retrofit: Retrofit = Retrofit.Builder()
        // The generated interfaces use relative paths ("me", "me/rounds"), which only
        // resolve against a base URL ending in a slash.
        .baseUrl(if (baseUrl.endsWith("/")) baseUrl else "$baseUrl/")
        .client(
            okHttpClient.newBuilder()
                .addInterceptor(AuthInterceptor(tokens))
                .authenticator(TokenAuthenticator(tokens))
                .build(),
        )
        // The generated models use @Contextual UUID and OffsetDateTime, which only the
        // generator's own Json instance knows how to read.
        .addConverterFactory(Serializer.kotlinxSerializationJson.asConverterFactory("application/json".toMediaType()))
        .build()

    /** A generated API interface, e.g. `service<RoundsApi>()`. */
    inline fun <reified T> service(): T = retrofit.create(T::class.java)
}
