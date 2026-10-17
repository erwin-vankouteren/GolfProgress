package com.golfprogress.app.practice

import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.app.rounds.parseWhole
import java.time.LocalDate

// The Log practice form's rules (#105), without Compose: the API's rules
// (CreatePracticeSessionRequest) and tags as chips. The same as the web app's
// practiceForm.ts.

const val MAX_DURATION_MINUTES = 1440
const val MAX_TAGS = 20
const val MAX_TAG_LENGTH = 50
const val MAX_SHORT_TEXT = 200
const val MAX_NOTES = 2000

enum class PracticeField(
    val apiName: String,
) {
    PracticedAt("practicedAt"),
    SessionType("sessionType"),
    DurationMinutes("durationMinutes"),
    VenueName("venueName"),
    PrimaryGoal("primaryGoal"),
    Tags("tags"),
    Notes("notes"),
}

data class PracticeFormValues(
    val practicedAt: LocalDate,
    val sessionType: CreatePracticeSessionRequest.SessionType = CreatePracticeSessionRequest.SessionType.Range,
    val durationMinutes: String = "",
    val venueName: String = "",
    val primaryGoal: String = "",
    val tags: List<String> = emptyList(),
    val notes: String = "",
)

sealed interface PracticeError {
    data object WholeNumber : PracticeError

    data class DurationRange(
        val max: Int,
    ) : PracticeError

    data class TooLong(
        val max: Int,
    ) : PracticeError

    data class TooManyTags(
        val max: Int,
    ) : PracticeError
}

/** Characters as people count them, after trimming. */
private fun length(text: String): Int = text.trim().let { it.codePointCount(0, it.length) }

fun validatePractice(values: PracticeFormValues): Map<PracticeField, PracticeError> =
    buildMap {
        if (values.durationMinutes.isNotBlank()) {
            val minutes = parseWhole(values.durationMinutes)
            when {
                minutes == null -> put(PracticeField.DurationMinutes, PracticeError.WholeNumber)
                minutes < 1 || minutes > MAX_DURATION_MINUTES ->
                    put(PracticeField.DurationMinutes, PracticeError.DurationRange(MAX_DURATION_MINUTES))
            }
        }
        if (length(values.venueName) > MAX_SHORT_TEXT) put(PracticeField.VenueName, PracticeError.TooLong(MAX_SHORT_TEXT))
        if (length(values.primaryGoal) > MAX_SHORT_TEXT) put(PracticeField.PrimaryGoal, PracticeError.TooLong(MAX_SHORT_TEXT))
        if (length(values.notes) > MAX_NOTES) put(PracticeField.Notes, PracticeError.TooLong(MAX_NOTES))
        if (values.tags.size > MAX_TAGS) put(PracticeField.Tags, PracticeError.TooManyTags(MAX_TAGS))
    }

sealed interface AddTag {
    data class Added(
        val tags: List<String>,
    ) : AddTag

    enum class Refused : AddTag { Empty, Duplicate, TooLong, TooMany }
}

/** A tag from the input: trimmed, at most 50 characters and 20 tags, and none twice in any case. */
fun addTag(
    tags: List<String>,
    input: String,
): AddTag {
    val tag = input.trim()
    return when {
        tag.isEmpty() -> AddTag.Refused.Empty
        tag.codePointCount(0, tag.length) > MAX_TAG_LENGTH -> AddTag.Refused.TooLong
        tags.any { it.equals(tag, ignoreCase = true) } -> AddTag.Refused.Duplicate
        tags.size >= MAX_TAGS -> AddTag.Refused.TooMany
        else -> AddTag.Added(tags + tag)
    }
}

/** The POST body. Optional text left empty is sent as null. */
fun PracticeFormValues.toRequest() =
    CreatePracticeSessionRequest(
        practicedAt = practicedAt.toString(),
        durationMinutes = parseWhole(durationMinutes),
        sessionType = sessionType,
        venueName = venueName.trim().ifEmpty { null },
        tags = tags,
        primaryGoal = primaryGoal.trim().ifEmpty { null },
        notes = notes.trim().ifEmpty { null },
    )
