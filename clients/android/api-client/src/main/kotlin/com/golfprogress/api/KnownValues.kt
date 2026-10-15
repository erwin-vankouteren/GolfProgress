package com.golfprogress.api

import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.generated.models.RoundResponse

/*
 * Open enums (#134): response fields the API may add values to at any time. The spec
 * publishes them as plain strings, so a value this build doesn't know never fails a
 * response; read them through these types, and show something generic for `Unknown`.
 *
 * The values mirror the spec's x-known-values; a test fails if they drift apart. All
 * other enum fields are closed and typed directly by the generated models.
 */

/** A practice session's type. `Unknown` for a type added to the API after this build. */
enum class SessionType(val apiValue: String?) {
    Range("Range"), Putting("Putting"), ShortGame("ShortGame"), Simulator("Simulator"),
    Lesson("Lesson"), Other("Other"), Unknown(null);

    companion object {
        fun of(value: String?): SessionType = entries.firstOrNull { it.apiValue != null && it.apiValue == value } ?: Unknown
    }
}

/** A micro-win's category. `Unknown` for a category added to the API after this build. */
enum class MicroWinCategory(val apiValue: String?) {
    StatDelta("StatDelta"), Behavior("Behavior"), Narrative("Narrative"), Habit("Habit"),
    Recovery("Recovery"), FocusArea("FocusArea"), Unknown(null);

    companion object {
        fun of(value: String?): MicroWinCategory = entries.firstOrNull { it.apiValue != null && it.apiValue == value } ?: Unknown
    }
}

/** Where a round came from. `Unknown` for a source added to the API after this build. */
enum class RoundSource(val apiValue: String?) {
    Manual("Manual"), Import("Import"), Coach("Coach"), Other("Other"), Unknown(null);

    companion object {
        fun of(value: String?): RoundSource = entries.firstOrNull { it.apiValue != null && it.apiValue == value } ?: Unknown
    }
}

val PracticeSessionResponse.knownSessionType: SessionType get() = SessionType.of(sessionType)
val MicroWinResponse.knownCategory: MicroWinCategory get() = MicroWinCategory.of(category)
val RoundResponse.knownSource: RoundSource get() = RoundSource.of(source)
