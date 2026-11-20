package com.golfprogress.app.feedback

import com.golfprogress.api.generated.models.CoachResponse
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import java.time.OffsetDateTime
import java.util.UUID

// The rules of feedback threads (#217), the same as the web app's feedback.ts.

/** What a thread is about; the names are the API's targetType values. */
enum class FeedbackTarget { Round, Reflection, PracticeSession }

/** The API's limit on a message, after trimming. */
const val FEEDBACK_TEXT_MAX = 2000

enum class FeedbackTextError { Empty, TooLong }

fun checkFeedbackText(text: String): FeedbackTextError? {
    val trimmed = text.trim()
    return when {
        trimmed.isEmpty() -> FeedbackTextError.Empty
        trimmed.length > FEEDBACK_TEXT_MAX -> FeedbackTextError.TooLong
        else -> null
    }
}

/** Whether a coach can see an item of this type, so the golfer can write to them about it. */
fun coachCanSee(
    coach: CoachResponse,
    target: FeedbackTarget,
): Boolean =
    when (target) {
        FeedbackTarget.Round -> true
        FeedbackTarget.Reflection -> coach.shareReflections
        FeedbackTarget.PracticeSession -> coach.sharePractice
    }

/** A coach's thread with the golfer on one item. */
data class GolferThread(
    /** Null for a coach whose account is gone. */
    val coachId: UUID?,
    /** Null for "a former coach". */
    val coachName: String?,
    val messages: List<FeedbackMessageResponse>,
    /** The coach is theirs and can see the item. */
    val canWrite: Boolean,
)

/**
 * One thread per coach: first each current coach who can see the item, even before anyone
 * has written; then any others with messages (a coach who stopped, or no longer sees this),
 * which can be read but not answered.
 */
fun golferThreads(
    messages: List<FeedbackMessageResponse>,
    coaches: List<CoachResponse>,
    target: FeedbackTarget,
): List<GolferThread> {
    val byCoach = messages.groupBy { it.coachId }
    val current =
        coaches.filter { coachCanSee(it, target) }.map {
            GolferThread(it.coachId, it.displayName, byCoach[it.coachId].orEmpty(), canWrite = true)
        }
    val currentIds = current.map { it.coachId }.toSet()
    val past =
        byCoach.filterKeys { it !in currentIds }.map { (coachId, list) ->
            GolferThread(coachId, list.first().coachName, list, canWrite = false)
        }
    return current + past
}

/** The newest message's time, to mark the threads read up to; null for none. */
fun newestMessageAt(messages: List<FeedbackMessageResponse>): OffsetDateTime? = messages.maxOfOrNull { it.createdAt }
