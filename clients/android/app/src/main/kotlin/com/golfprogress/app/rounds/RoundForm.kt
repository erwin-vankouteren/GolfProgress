package com.golfprogress.app.rounds

import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.UserResponse
import java.time.LocalDate
import kotlin.math.roundToInt

// The Log round form's rules (#94), without Compose: which fields a tracking mode shows,
// validation with the API's rules (CreateRoundRequest), and the request it sends. The
// same rules as the web app's roundForm.ts.

/** The optional stats, with the API's name for each in a validation problem. */
enum class StatField(
    val apiName: String,
    /** Counted at most once per hole, so capped at the hole count. */
    val perHole: Boolean,
) {
    TotalPutts("totalPutts", perHole = false),
    Penalties("penalties", perHole = false),
    FairwaysHit("fairwaysHit", perHole = true),
    Gir("gir", perHole = true),
    ThreePutts("threePutts", perHole = true),
    BlowUpHoles("blowUpHoles", perHole = true),
}

/** Every field with an error to show; the API's names for them. */
enum class RoundField(
    val apiName: String,
) {
    PlayedAt("playedAt"),
    Holes("holes"),
    CourseName("courseName"),
    TeeName("teeName"),
    TotalStrokes("totalStrokes"),
    TotalPutts("totalPutts"),
    Penalties("penalties"),
    FairwaysHit("fairwaysHit"),
    Gir("gir"),
    ThreePutts("threePutts"),
    BlowUpHoles("blowUpHoles"),
    Notes("notes"),
    ;

    companion object {
        fun of(stat: StatField): RoundField = valueOf(stat.name)
    }
}

/** What the inputs hold: text, so a half-typed or cleared number is representable. */
data class RoundFormValues(
    val playedAt: LocalDate,
    val holes: Int = 18,
    val courseName: String = "",
    val teeName: String = "",
    val totalStrokes: String = "",
    val stats: Map<StatField, String> = emptyMap(),
    val notes: String = "",
) {
    fun stat(field: StatField): String = stats[field].orEmpty()
}

/**
 * The optional stats each tracking mode shows (docs/clients/ux-design.md → "Rounds"):
 * Minimal none, Balanced putts and penalties, Detailed all six. A mode this build doesn't
 * know shows the most, so nothing a user tracks is hidden.
 */
fun statFieldsFor(mode: UserResponse.TrackingMode?): List<StatField> =
    when (mode) {
        UserResponse.TrackingMode.Minimal -> emptyList()
        UserResponse.TrackingMode.Balanced -> listOf(StatField.TotalPutts, StatField.Penalties)
        else -> StatField.entries
    }

/** The most a stat can be with the current values, for "out of 18" and the stepper. */
fun maxFor(
    field: StatField,
    values: RoundFormValues,
): Int? {
    if (field.perHole) return values.holes
    val strokes = parseWhole(values.totalStrokes)
    return if (strokes != null && strokes > 0) strokes else null
}

sealed interface RoundError {
    data object StrokesRequired : RoundError

    data object WholeNumber : RoundError

    data object StrokesPositive : RoundError

    data object NotNegative : RoundError

    data class AtMostHoles(
        val max: Int,
    ) : RoundError

    data class AtMostStrokes(
        val max: Int,
    ) : RoundError
}

/** The API's rules, checked on the fields the form shows. */
fun validateRound(
    values: RoundFormValues,
    stats: List<StatField>,
): Map<RoundField, RoundError> =
    buildMap {
        val strokes = parseWhole(values.totalStrokes)
        when {
            values.totalStrokes.isBlank() -> put(RoundField.TotalStrokes, RoundError.StrokesRequired)
            strokes == null -> put(RoundField.TotalStrokes, RoundError.WholeNumber)
            strokes <= 0 -> put(RoundField.TotalStrokes, RoundError.StrokesPositive)
        }

        for (field in stats) {
            val text = values.stat(field).trim()
            if (text.isEmpty()) continue // not tracked this round
            val value = parseWhole(text)
            val error =
                when {
                    value == null -> RoundError.WholeNumber
                    value < 0 -> RoundError.NotNegative
                    field.perHole && value > values.holes -> RoundError.AtMostHoles(values.holes)
                    !field.perHole && strokes != null && strokes > 0 && value > strokes -> RoundError.AtMostStrokes(strokes)
                    else -> null
                }
            if (error != null) put(RoundField.of(field), error)
        }
    }

/** The POST /me/rounds body. Stats the form doesn't show, or left empty, aren't tracked. */
fun toRequest(
    values: RoundFormValues,
    stats: List<StatField>,
): CreateRoundRequest {
    fun stat(field: StatField) = if (field in stats) parseWhole(values.stat(field)) else null
    return CreateRoundRequest(
        playedAt = values.playedAt.toString(),
        totalStrokes = parseWhole(values.totalStrokes) ?: 0,
        holes = values.holes,
        courseName = values.courseName.trim().ifEmpty { null },
        teeName = values.teeName.trim().ifEmpty { null },
        totalPutts = stat(StatField.TotalPutts),
        penalties = stat(StatField.Penalties),
        fairwaysHit = stat(StatField.FairwaysHit),
        gir = stat(StatField.Gir),
        threePutts = stat(StatField.ThreePutts),
        blowUpHoles = stat(StatField.BlowUpHoles),
        notes = values.notes.trim().ifEmpty { null },
    )
}

/**
 * The next value when a stepper is pressed. From empty it starts somewhere useful
 * rather than at zero: strokes at the user's typical score (or 90) for the hole count,
 * putts at two a hole, the rest at zero.
 */
fun stepStrokes(
    values: RoundFormValues,
    delta: Int,
    typical18Score: Int?,
): String {
    val current = parseWhole(values.totalStrokes) ?: return ((typical18Score ?: 90) * values.holes / 18.0).roundToInt().toString()
    return maxOf(1, current + delta).toString()
}

fun stepStat(
    field: StatField,
    values: RoundFormValues,
    delta: Int,
): String {
    val current = parseWhole(values.stat(field)) ?: return (if (field == StatField.TotalPutts) values.holes * 2 else 0).toString()
    val next = maxOf(0, current + delta)
    return (maxFor(field, values)?.let { minOf(it, next) } ?: next).toString()
}

/** A whole number, or null for empty or anything else ("1.5", "abc"). */
fun parseWhole(text: String): Int? {
    val trimmed = text.trim()
    return if (Regex("""-?\d{1,9}""").matches(trimmed)) trimmed.toInt() else null
}
