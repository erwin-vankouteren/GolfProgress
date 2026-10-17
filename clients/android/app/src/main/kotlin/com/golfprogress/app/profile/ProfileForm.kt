package com.golfprogress.app.profile

import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.Change
import com.golfprogress.app.data.ProfileChanges

// The profile's rules (#113), without Compose: what PATCH /me and POST /me/password
// accept, checked before sending. The same as the web app's profileForm.ts.

/** User.DisplayNameMaxLength. */
const val MAX_DISPLAY_NAME = 100

/** UpdateMeRequest.MaxGoalFocusAreas. */
const val MAX_GOAL_FOCUS = 3
const val HANDICAP_MIN = -10.0
const val HANDICAP_MAX = 54.0
const val TYPICAL_MIN = 18
const val TYPICAL_MAX = 200
const val PASSWORD_MIN = 8
const val PASSWORD_MAX = 128

/** GoalFocusArea in the API, in its order. */
enum class GoalArea { Putting, Penalties, TeeShots, Consistency }

private fun String.characters(): Int = codePointCount(0, length)

enum class ProfileField(
    val apiName: String,
) {
    DisplayName("displayName"),
    HandicapIndex("handicapIndex"),
    Typical18Score("typical18Score"),
}

data class ProfileValues(
    val displayName: String = "",
    val handicapIndex: String = "",
    val typical18Score: String = "",
)

sealed interface ProfileError {
    data object Required : ProfileError

    data class TooLong(
        val max: Int,
    ) : ProfileError

    data object NotANumber : ProfileError

    data object NotWhole : ProfileError

    data class OutOfRange(
        val min: Number,
        val max: Number,
    ) : ProfileError
}

fun profileValuesOf(me: UserResponse) =
    ProfileValues(
        displayName = me.displayName.orEmpty(),
        handicapIndex = me.handicapIndex?.let { if (it % 1.0 == 0.0) it.toInt().toString() else it.toString() }.orEmpty(),
        typical18Score = me.typical18Score?.toString().orEmpty(),
    )

/** A decimal as typed, with a point or a comma; null when it isn't one. */
fun parseDecimal(text: String): Double? {
    val normalised = text.trim().replace(',', '.')
    return if (Regex("""[+-]?(\d+\.?\d*|\.\d+)""").matches(normalised)) normalised.toDouble() else null
}

fun validateProfile(values: ProfileValues): Map<ProfileField, ProfileError> =
    buildMap {
        val name = values.displayName.trim()
        when {
            name.isEmpty() -> put(ProfileField.DisplayName, ProfileError.Required)
            name.characters() > MAX_DISPLAY_NAME -> put(ProfileField.DisplayName, ProfileError.TooLong(MAX_DISPLAY_NAME))
        }

        if (values.handicapIndex.isNotBlank()) {
            val handicap = parseDecimal(values.handicapIndex)
            when {
                handicap == null -> put(ProfileField.HandicapIndex, ProfileError.NotANumber)
                handicap < HANDICAP_MIN || handicap > HANDICAP_MAX ->
                    put(ProfileField.HandicapIndex, ProfileError.OutOfRange(HANDICAP_MIN.toInt(), HANDICAP_MAX.toInt()))
            }
        }

        if (values.typical18Score.isNotBlank()) {
            val score = parseDecimal(values.typical18Score)
            when {
                score == null -> put(ProfileField.Typical18Score, ProfileError.NotANumber)
                score % 1.0 != 0.0 -> put(ProfileField.Typical18Score, ProfileError.NotWhole)
                score < TYPICAL_MIN || score > TYPICAL_MAX ->
                    put(
                        ProfileField.Typical18Score,
                        ProfileError.OutOfRange(TYPICAL_MIN, TYPICAL_MAX),
                    )
            }
        }
    }

/** PATCH /me for the details: an emptied handicap or typical score is cleared. */
fun ProfileValues.toChanges() =
    ProfileChanges(
        displayName = displayName.trim(),
        handicapIndex = Change(parseDecimal(handicapIndex)),
        typical18Score = Change(parseDecimal(typical18Score)?.toInt()),
    )

data class Preferences(
    val units: UserResponse.Units = UserResponse.Units.Metric,
    val trackingMode: UserResponse.TrackingMode = UserResponse.TrackingMode.Minimal,
    val goalFocus: List<GoalArea> = emptyList(),
) {
    fun toChanges() =
        ProfileChanges(
            units = units.value,
            trackingMode = trackingMode.value,
            goalFocus = goalFocus.map { it.name },
        )
}

/** The user's preferences; goal areas this build doesn't know are left out. */
fun preferencesOf(me: UserResponse) =
    Preferences(
        units = me.units,
        trackingMode = me.trackingMode,
        goalFocus = GoalArea.entries.filter { area -> me.goalFocus.any { it.equals(area.name, ignoreCase = true) } },
    )

/** Ticks or unticks an area, in the API's order; no more than three can be ticked. */
fun toggleGoal(
    current: List<GoalArea>,
    area: GoalArea,
): List<GoalArea> =
    when {
        area in current -> current - area
        current.size >= MAX_GOAL_FOCUS -> current
        else -> GoalArea.entries.filter { it == area || it in current }
    }

sealed interface PasswordError {
    data object Required : PasswordError

    data class TooShort(
        val min: Int,
    ) : PasswordError

    data class TooLong(
        val max: Int,
    ) : PasswordError

    data object OnlySpaces : PasswordError

    data object IsEmail : PasswordError

    data object Unchanged : PasswordError
}

/** PasswordPolicy.Check, plus "different from the current one". */
fun checkNewPassword(
    password: String,
    current: String,
    email: String?,
): PasswordError? {
    val length = password.characters()
    return when {
        length == 0 -> PasswordError.Required
        length < PASSWORD_MIN -> PasswordError.TooShort(PASSWORD_MIN)
        length > PASSWORD_MAX -> PasswordError.TooLong(PASSWORD_MAX)
        password.isBlank() -> PasswordError.OnlySpaces
        email != null && password.trim().equals(email.trim(), ignoreCase = true) -> PasswordError.IsEmail
        password == current -> PasswordError.Unchanged
        else -> null
    }
}
