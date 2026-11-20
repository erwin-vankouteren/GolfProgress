package com.golfprogress.app.feedback

import com.golfprogress.api.generated.models.CoachResponse
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import com.golfprogress.api.generated.models.UnreadFeedbackResponse
import com.golfprogress.api.generated.models.UnreadFeedbackThreadResponse
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

/** Unread messages in threads the Home card doesn't list, which shows at most five (#222). */
fun unreadNotListed(unread: UnreadFeedbackResponse): Int = maxOf(0, unread.count - unread.threads.sumOf { it.unread })

/** Where an unread thread is: its round (also for a reflection), its session, or nowhere to go. */
sealed interface UnreadDestination {
    data class Round(
        val roundId: UUID,
    ) : UnreadDestination

    data class Session(
        val sessionId: UUID,
    ) : UnreadDestination
}

fun destinationOf(thread: UnreadFeedbackThreadResponse): UnreadDestination? =
    when {
        thread.targetType == UnreadFeedbackThreadResponse.TargetType.PracticeSession -> UnreadDestination.Session(thread.targetId)
        else -> thread.roundId?.let { UnreadDestination.Round(it) }
    }

/** The newest message's time, to mark the threads read up to; null for none. */
fun newestMessageAt(messages: List<FeedbackMessageResponse>): OffsetDateTime? = messages.maxOfOrNull { it.createdAt }
