package com.golfprogress.app.goals

import com.golfprogress.api.generated.models.PracticeGoalResponse
import java.time.LocalDate
import java.util.UUID

// Practice goals' rules (#231), the same as the web app's goals.ts.

/** Still worth showing on Practice: active, or ended without being marked done or dismissed. */
fun goalsToShow(goals: List<PracticeGoalResponse>): List<PracticeGoalResponse> = goals.filter { it.isOpen }

/** Whether the golfer can still mark it done or dismiss it. */
val PracticeGoalResponse.isOpen: Boolean
    get() = status == PracticeGoalResponse.Status.Active || status == PracticeGoalResponse.Status.Ended

/** The goals a session on [practicedAt] can count towards: active, and the date within their period. */
fun linkableGoals(
    goals: List<PracticeGoalResponse>,
    practicedAt: LocalDate,
): List<PracticeGoalResponse> =
    goals.filter {
        it.status == PracticeGoalResponse.Status.Active &&
            LocalDate.parse(it.startsOn) <= practicedAt &&
            practicedAt <= LocalDate.parse(it.endsOn)
    }

/** The goal to suggest for a session: the first linkable one set for that type of session. */
fun suggestedGoal(
    goals: List<PracticeGoalResponse>,
    practicedAt: LocalDate,
    sessionType: String,
): PracticeGoalResponse? = linkableGoals(goals, practicedAt).firstOrNull { it.sessionType == sessionType }

/**
 * The goal a session counts towards: the one the golfer chose while it still applies, or
 * until they choose, the suggestion. [chosen] is null before choosing; "no goal" is [NO_GOAL].
 */
fun selectedGoal(
    goals: List<PracticeGoalResponse>,
    practicedAt: LocalDate,
    sessionType: String,
    chosen: UUID?,
): UUID? =
    when (chosen) {
        null -> suggestedGoal(goals, practicedAt, sessionType)?.practiceGoalId
        NO_GOAL -> null
        else -> chosen.takeIf { id -> linkableGoals(goals, practicedAt).any { it.practiceGoalId == id } }
    }

/** The golfer chose "No goal". */
val NO_GOAL: UUID = UUID(0, 0)

/** The target is reached. */
val PracticeGoalResponse.reached: Boolean get() = linkedSessions >= targetSessions
