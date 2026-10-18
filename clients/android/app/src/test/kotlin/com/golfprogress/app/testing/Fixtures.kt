package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.DashboardRoundSummary
import com.golfprogress.api.generated.models.FitnessScoreExplanation
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.MonthlyReviewResponse
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.UserBaselineResponse
import com.golfprogress.api.generated.models.UserResponse
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

    fun user(
        trackingMode: UserResponse.TrackingMode = UserResponse.TrackingMode.Balanced,
        typical18Score: Int? = 92,
        goalFocus: List<String> = listOf("Putting"),
        language: String? = null,
    ) = UserResponse(
        userId = userId,
        email = "erwin@example.test",
        displayName = "Erwin",
        timezone = "Europe/Amsterdam",
        units = UserResponse.Units.Metric,
        trackingMode = trackingMode,
        language = language,
        handicapIndex = 18.4,
        typical18Score = typical18Score,
        goalFocus = goalFocus,
        createdAt = now,
        updatedAt = now,
    )

    /** A round as the API returns it after saving [request], scored 72. */
    fun round(
        request: CreateRoundRequest,
        roundId: UUID = roundId(9),
    ) = RoundResponse(
        roundId = roundId,
        userId = userId,
        playedAt = request.playedAt,
        holes = request.holes ?: 18,
        courseName = request.courseName,
        teeName = request.teeName,
        totalStrokes = request.totalStrokes,
        totalPutts = request.totalPutts,
        penalties = request.penalties,
        fairwaysHit = request.fairwaysHit,
        gir = request.gir,
        threePutts = request.threePutts,
        blowUpHoles = request.blowUpHoles,
        source = "Manual",
        notes = request.notes,
        createdAt = now,
        updatedAt = now,
        fitnessScore = 72,
        fitnessConfidence = RoundResponse.FitnessConfidence.Low,
    )

    /** A saved round with id [i], played [i] days before 20 May 2026. */
    fun savedRound(
        i: Int,
        strokes: Int = 88,
        holes: Int = 18,
        course: String? = "Hilversum",
        penalties: Int? = null,
        putts: Int? = null,
        blowUps: Int? = null,
    ) = round(
        CreateRoundRequest(
            playedAt =
                java.time.LocalDate
                    .of(2026, 5, 20)
                    .minusDays(i.toLong())
                    .toString(),
            totalStrokes = strokes,
            holes = holes,
            courseName = course,
            totalPutts = putts,
            penalties = penalties,
            blowUpHoles = blowUps,
        ),
        roundId(i),
    )

    fun practiceSession(
        i: Int,
        request: CreatePracticeSessionRequest =
            CreatePracticeSessionRequest(
                practicedAt =
                    java.time.LocalDate
                        .of(2026, 5, 20)
                        .minusDays(i.toLong())
                        .toString(),
                durationMinutes = 45,
                sessionType = CreatePracticeSessionRequest.SessionType.Putting,
            ),
        sessionType: String = request.sessionType?.value ?: "Range",
    ) = PracticeSessionResponse(
        practiceSessionId = UUID.fromString("00000000-0000-0000-0001-%012d".format(i)),
        userId = userId,
        practicedAt = request.practicedAt,
        durationMinutes = request.durationMinutes,
        sessionType = sessionType,
        venueName = request.venueName,
        tags = request.tags.orEmpty(),
        primaryGoal = request.primaryGoal,
        notes = request.notes,
        createdAt = now,
    )

    fun timelinePoint(
        i: Int,
        playedAt: String,
        score: Int?,
        holes: Int = 18,
    ) = RoundTimelinePointResponse(
        roundId = roundId(i),
        playedAt = playedAt,
        holes = holes,
        totalStrokes = if (holes == 18) 90 - i else 45,
        totalPutts = null,
        penalties = null,
        threePutts = null,
        blowUpHoles = null,
        fitnessScoreTotal = score,
        fitnessConfidence = score?.let { RoundTimelinePointResponse.FitnessConfidence.Medium },
        microWinTitle = null,
    )

    fun timeline() =
        listOf(
            timelinePoint(1, "2026-08-02", 55),
            timelinePoint(2, "2026-09-06", 60),
            timelinePoint(3, "2026-10-04", 64, holes = 9),
            timelinePoint(4, "2026-10-11", 70),
        )

    fun month(
        monthStart: String,
        average: Double?,
        rounds: Int = if (average == null) 0 else 2,
    ) = MonthlyTimelinePointResponse(monthStart, average, null, rounds, emptyList(), false)

    fun months() = listOf(month("2026-08-01", 55.0, 1), month("2026-09-01", null), month("2026-10-01", 67.0))

    fun baseline() =
        UserBaselineResponse(
            userId = userId,
            windowRounds = 5,
            sampleSize = 4,
            avgScore = 88.25,
            scoreStdDev = 3.4,
            avgPutts = 33.5,
            avgPenalties = null,
            avgThreePutts = null,
            avgBlowUpHoles = null,
            avgFairwaysHit = null,
            avgGir = null,
            completenessRatio = 0.4,
            confidenceLevel = UserBaselineResponse.ConfidenceLevel.Medium,
            computedAt = now,
        )

    fun review(
        monthStart: String,
        focus: String? = null,
    ) = MonthlyReviewResponse(
        monthlyReviewId = UUID.nameUUIDFromBytes(monthStart.toByteArray()),
        userId = userId,
        monthStart = monthStart,
        fitnessAvg = 67.0,
        fitnessChange = 7.0,
        focusNextMonth = focus,
        roundsLogged = 2,
        topMicroWinCategory = "Habit",
        highlights = emptyList(),
        computedAt = now,
    )

    fun reflection(
        roundId: UUID = roundId(1),
        focus: Int? = 4,
        whatWorked: String? = "Tempo on the tee",
    ) = ReflectionResponse(
        reflectionId = UUID.nameUUIDFromBytes("$roundId$focus$whatWorked".toByteArray()),
        userId = userId,
        roundId = roundId,
        focusRating = focus,
        moodRating = null,
        frustrationSpillover = false,
        whatWorked = whatWorked,
        whatToRepeat = null,
        oneChangeNext = null,
        createdAt = now,
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
