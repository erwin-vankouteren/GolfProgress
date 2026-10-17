package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.DashboardRoundSummary
import com.golfprogress.api.generated.models.FitnessScoreExplanation
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import java.time.OffsetDateTime
import java.util.UUID

/** API responses for tests, with sensible defaults to override. */
object Fixtures {
    val userId: UUID = UUID.fromString("3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f")
    val now: OffsetDateTime = OffsetDateTime.parse("2026-05-10T10:00:00Z")

    fun roundId(i: Int): UUID = UUID.fromString("00000000-0000-0000-0000-%012d".format(i))

    fun lastRound(
        roundId: UUID = roundId(1),
        playedAt: String = "2026-05-03",
        strokes: Int = 88,
        score: Int? = 72,
        confidence: DashboardRoundSummary.FitnessConfidence? = DashboardRoundSummary.FitnessConfidence.Medium,
    ) = DashboardRoundSummary(roundId, playedAt, strokes, score, confidence)

    fun nextLever(roundId: UUID = roundId(1)) =
        NextLeverResponse(
            roundId = roundId,
            userId = userId,
            leverKey = "three_putts",
            title = "Cut down three-putts",
            recommendation = "On long putts, aim for a one-metre circle: pace first.",
            rationale = "Three-putts cost you most strokes last round.",
            confidenceLevel = NextLeverResponse.ConfidenceLevel.Medium,
            createdAt = now,
        )

    fun microWin(
        title: String,
        roundId: UUID = roundId(1),
    ) = MicroWinResponse(
        microWinId = UUID.nameUUIDFromBytes(title.toByteArray()),
        roundId = roundId,
        userId = userId,
        category = "StatDelta",
        metric = null,
        direction = MicroWinResponse.Direction.Improved,
        deltaValue = null,
        confidenceLevel = MicroWinResponse.ConfidenceLevel.Medium,
        title = title,
        message = "",
        createdAt = now,
    )

    fun dashboard(
        lastRound: DashboardRoundSummary? = lastRound(),
        nextLever: NextLeverResponse? = nextLever(),
        microWins: List<MicroWinResponse> = listOf(microWin("Fewer penalties"), microWin("Learning captured")),
        baselineAvgScore: Double? = 91.43,
        baselineConfidence: DashboardResponse.BaselineConfidence? = DashboardResponse.BaselineConfidence.Low,
        monthlyFitnessAvg: Double? = 70.0,
        monthlyFitnessChange: Double? = 4.0,
    ) = DashboardResponse(
        userId = userId,
        lastRound = lastRound,
        microWins = microWins,
        nextLever = nextLever,
        baselineAvgScore = baselineAvgScore,
        baselineConfidence = baselineConfidence,
        monthlyFitnessAvg = monthlyFitnessAvg,
        monthlyFitnessChange = monthlyFitnessChange,
        generatedAt = now,
    )

    /** A new user: nothing logged yet. */
    fun emptyDashboard() =
        dashboard(
            lastRound = null,
            nextLever = null,
            microWins = emptyList(),
            baselineAvgScore = null,
            baselineConfidence = null,
            monthlyFitnessAvg = null,
            monthlyFitnessChange = null,
        )

    fun fitnessScore(
        roundId: UUID = roundId(1),
        execution: Int = 18,
        damageControl: Int = 21,
        consistency: Int = 23,
        habits: Int = 10,
    ) = FitnessScoreResponse(
        fitnessScoreId = UUID.randomUUID(),
        userId = userId,
        roundId = roundId,
        scoreTotal = execution + damageControl + consistency + habits,
        execution = execution,
        damageControl = damageControl,
        consistency = consistency,
        habits = habits,
        confidenceLevel = FitnessScoreResponse.ConfidenceLevel.Medium,
        computedAt = now,
        explanation = FitnessScoreExplanation(90.0, 3.0, 3, 1, false),
    )
}
