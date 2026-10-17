package com.golfprogress.app.progress

import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import java.time.LocalDate
import kotlin.math.roundToInt

// The Progress section's rules (#109), without Compose: the date ranges, what the charts
// plot, and when there's enough for a trend. The same as the web app's progress.ts.

/** Rounds the timeline asks for: the API's maximum. */
const val TIMELINE_TAKE = 100

/** Months in the monthly trend, and in the review's month picker. */
const val TREND_MONTHS = 12

/** Scored rounds needed before the timeline draws a line. */
const val TREND_MINIMUM_ROUNDS = 3

/** Months with an average needed before the monthly trend draws bars. */
const val TREND_MINIMUM_MONTHS = 2

/** CreateMonthlyReviewRequest's focus limit. */
const val MAX_FOCUS_LENGTH = 200

enum class RangeKey { ThreeMonths, SixMonths, TwelveMonths, AllTime, Custom }

data class DateRange(
    val from: LocalDate? = null,
    val to: LocalDate? = null,
)

/** GET /me/timeline's from and to; a month back from 31 May is the end of February. */
fun rangeQuery(
    key: RangeKey,
    today: LocalDate,
    custom: DateRange,
): DateRange =
    when (key) {
        RangeKey.ThreeMonths -> DateRange(from = today.minusMonths(3))
        RangeKey.SixMonths -> DateRange(from = today.minusMonths(6))
        RangeKey.TwelveMonths -> DateRange(from = today.minusMonths(12))
        RangeKey.AllTime -> DateRange()
        RangeKey.Custom -> custom
    }

/** A custom range ending before it starts can't be asked for. */
fun DateRange.isValid(): Boolean = from == null || to == null || !from.isAfter(to)

data class TimelineChart(
    /** One point per round, oldest first; x is the index. */
    val fitness: List<Pair<Int, Int>>,
    /** Strokes for rounds of [strokesHoles] holes only, so 9 and 18 aren't one line. */
    val strokes: List<Pair<Int, Int>>,
    val strokesHoles: Int?,
)

/**
 * What the timeline chart plots. Strokes for 9 and 18 holes aren't comparable, so the
 * strokes line shows 18-hole rounds, or 9-hole ones when there are no 18-hole rounds.
 */
fun timelineChart(points: List<RoundTimelinePointResponse>): TimelineChart {
    val holes =
        if (points.any { it.holes == 18 }) {
            18
        } else if (points.any { it.holes == 9 }) {
            9
        } else {
            null
        }
    return TimelineChart(
        fitness = points.mapIndexedNotNull { i, p -> p.fitnessScoreTotal?.let { i to it } },
        strokes = points.mapIndexedNotNull { i, p -> if (p.holes == holes) i to p.totalStrokes else null },
        strokesHoles = holes,
    )
}

/** Whether the strokes line leaves out some rounds, which the chart then says. */
fun strokesLeaveOutRounds(points: List<RoundTimelinePointResponse>): Boolean = points.map { it.holes }.toSet().size > 1

data class ScoreSummary(
    val first: Int,
    val last: Int,
    val average: Int,
)

data class TimelineSummary(
    val rounds: Int,
    val firstDate: String,
    val lastDate: String,
    val score: ScoreSummary?,
)

/** The sentence that stands in for the timeline chart; null without rounds. */
fun timelineSummary(points: List<RoundTimelinePointResponse>): TimelineSummary? {
    if (points.isEmpty()) return null
    val scores = points.mapNotNull { it.fitnessScoreTotal }
    return TimelineSummary(
        rounds = points.size,
        firstDate = points.first().playedAt,
        lastDate = points.last().playedAt,
        score = if (scores.isEmpty()) null else ScoreSummary(scores.first(), scores.last(), scores.average().roundToInt()),
    )
}

/** How many more scored rounds before there's a trend to draw; 0 when there is one. */
fun roundsShortOfTrend(points: List<RoundTimelinePointResponse>): Int =
    maxOf(0, TREND_MINIMUM_ROUNDS - points.count { it.fitnessScoreTotal != null })

data class MonthAverage(
    val month: String,
    val average: Int,
)

data class MonthlySummary(
    val months: Int,
    val first: MonthAverage,
    val last: MonthAverage,
)

/** The sentence that stands in for the monthly chart; null without any month's average. */
fun monthlySummary(items: List<MonthlyTimelinePointResponse>): MonthlySummary? {
    val averaged = items.mapNotNull { item -> item.fitnessAvg?.let { MonthAverage(item.monthStart, it.roundToInt()) } }
    if (averaged.isEmpty()) return null
    return MonthlySummary(averaged.size, averaged.first(), averaged.last())
}

fun hasMonthlyTrend(items: List<MonthlyTimelinePointResponse>): Boolean = items.count { it.fitnessAvg != null } >= TREND_MINIMUM_MONTHS

/** The review opens on the latest month with rounds, or this month when there are none. */
fun defaultReviewMonth(items: List<MonthlyTimelinePointResponse>): String? =
    (items.lastOrNull { it.roundsLogged > 0 } ?: items.lastOrNull())?.monthStart

/** The focus as sent: trimmed, blank as nothing; null result when too long. */
sealed interface CheckedFocus {
    data class Ok(
        val value: String?,
    ) : CheckedFocus

    data object TooLong : CheckedFocus
}

fun checkFocus(focus: String): CheckedFocus {
    val trimmed = focus.trim()
    return when {
        trimmed.codePointCount(0, trimmed.length) > MAX_FOCUS_LENGTH -> CheckedFocus.TooLong
        trimmed.isEmpty() -> CheckedFocus.Ok(null)
        else -> CheckedFocus.Ok(trimmed)
    }
}
