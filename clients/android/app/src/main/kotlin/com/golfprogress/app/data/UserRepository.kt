package com.golfprogress.app.data

import com.golfprogress.api.AccountApi
import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.UsersApi
import com.golfprogress.api.generated.models.ChangePasswordRequest
import com.golfprogress.api.generated.models.DeleteAccountRequest
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.api.unwrap
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import javax.inject.Inject

/** A new value for a field, null included: `Change(null)` clears it. */
data class Change<out T>(
    val value: T,
)

/**
 * What PATCH /me changes (#113): a field left null stays as it is. The optional numbers
 * are [Change]s, so one can be cleared rather than left alone.
 */
data class ProfileChanges(
    val displayName: String? = null,
    val timezone: String? = null,
    val units: String? = null,
    val trackingMode: String? = null,
    val goalFocus: List<String>? = null,
    val handicapIndex: Change<Double?>? = null,
    val typical18Score: Change<Int?>? = null,
)

/** The signed-in user's profile, preferences and account (#94, #113). */
interface UserRepository {
    suspend fun me(): UserResponse

    suspend fun update(changes: ProfileChanges): UserResponse

    /** A wrong current password is a 400 on currentPassword; the user stays signed in. */
    suspend fun changePassword(
        current: String,
        new: String,
    )

    /** Deletes the account and everything in it (#84). A wrong password is a 400. */
    suspend fun deleteAccount(password: String)
}

class ApiUserRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : UserRepository {
        override suspend fun me(): UserResponse = api.service<UsersApi>().getMe().unwrap()

        override suspend fun update(changes: ProfileChanges): UserResponse {
            val body =
                buildJsonObject {
                    changes.displayName?.let { put("displayName", it) }
                    changes.timezone?.let { put("timezone", it) }
                    changes.units?.let { put("units", it) }
                    changes.trackingMode?.let { put("trackingMode", it) }
                    changes.goalFocus?.let { areas -> put("goalFocus", buildJsonArray { areas.forEach { add(JsonPrimitive(it)) } }) }
                    changes.handicapIndex?.let { put("handicapIndex", it.value?.let(::JsonPrimitive) ?: JsonNull) }
                    changes.typical18Score?.let { put("typical18Score", it.value?.let(::JsonPrimitive) ?: JsonNull) }
                }
            return api.service<AccountApi>().updateMe(body).unwrap()
        }

        override suspend fun changePassword(
            current: String,
            new: String,
        ) {
            api.service<UsersApi>().changePassword(ChangePasswordRequest(current, new)).unwrap()
        }

        override suspend fun deleteAccount(password: String) {
            api.service<AccountApi>().deleteMe(DeleteAccountRequest(password)).unwrap()
        }
    }
