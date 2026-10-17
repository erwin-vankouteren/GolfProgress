package com.golfprogress.api

import com.golfprogress.api.generated.models.DeleteAccountRequest
import com.golfprogress.api.generated.models.UserResponse
import kotlinx.serialization.json.JsonObject
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.HTTP
import retrofit2.http.PATCH

/**
 * Two account calls the generated UsersApi can't make correctly (#113). Use these
 * instead of UsersApi.updateMe and UsersApi.deleteMe.
 *
 * - **PATCH /me** leaves out what isn't sent and clears what is sent as null (#82). The
 *   generated UpdateMeRequest encodes every field, nulls included, so changing only the
 *   units would also clear the handicap. Here the body is exactly the fields given; build
 *   it with `buildJsonObject`.
 * - **DELETE /me** carries the password in its body (#84). The generated call pairs
 *   `@DELETE` with `@Body`, which Retrofit refuses at runtime; `@HTTP` with `hasBody` is
 *   how Retrofit sends a body on DELETE.
 */
interface AccountApi {
    @PATCH("me")
    suspend fun updateMe(
        @Body fields: JsonObject,
    ): Response<UserResponse>

    @HTTP(method = "DELETE", path = "me", hasBody = true)
    suspend fun deleteMe(
        @Body request: DeleteAccountRequest,
    ): Response<Unit>
}
