package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.CreateReflectionRequest
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.DashboardResponse
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
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.data.Page
import com.golfprogress.app.data.PracticeRepository
import com.golfprogress.app.data.ProgressRepository
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.data.UserRepository
import com.golfprogress.app.di.RepositoryModule
import dagger.Module
import dagger.Provides
import dagger.hilt.components.SingletonComponent
import dagger.hilt.testing.TestInstallIn
import java.util.UUID

/*
 * In-memory repositories for tests. View-model tests construct them directly; Hilt UI
 * tests get them in place of the real ones (FakeRepositoryModule) and set them up
 * through FakeRepositories before showing a screen.
 */

class FakeDashboardRepository(
    var dashboard: () -> DashboardResponse = { Fixtures.dashboard() },
    var scores: Map<UUID, FitnessScoreResponse> = mapOf(Fixtures.roundId(1) to Fixtures.fitnessScore()),
) : DashboardRepository {
    var dashboardCalls = 0
        private set

    override suspend fun dashboard(): DashboardResponse {
        dashboardCalls++
        return dashboard.invoke()
    }

    override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? = scores[roundId]
}

class FakeUserRepository(
    var me: () -> UserResponse = { Fixtures.user() },
) : UserRepository {
    override suspend fun me(): UserResponse = me.invoke()
}

/**
 * Rounds in memory, newest first. Creating answers with [respond] (by default the round
 * as saved, id 9); the rest read and change [rounds]. [failWith] makes every call throw.
 */
class FakeRoundsRepository(
    rounds: List<RoundResponse> = emptyList(),
    var respond: (CreateRoundRequest) -> RoundResponse = { Fixtures.round(it) },
) : RoundsRepository {
    val rounds = rounds.toMutableList()
    val created = mutableListOf<CreateRoundRequest>()
    val updated = mutableListOf<Pair<UUID, CreateRoundRequest>>()
    val deleted = mutableListOf<UUID>()
    val pages = mutableListOf<Pair<Int, Int>>()
    var failWith: Exception? = null
    var score: (UUID) -> FitnessScoreResponse? = { Fixtures.fitnessScore(it) }
    var microWins: List<MicroWinResponse> = listOf(Fixtures.microWin("Fewer penalties"))
    var reflections: List<ReflectionResponse> = emptyList()

    private fun check() = failWith?.let { throw it }

    override suspend fun create(request: CreateRoundRequest): RoundResponse {
        check()
        created += request
        return respond(request).also { rounds.add(0, it) }
    }

    override suspend fun update(
        roundId: UUID,
        request: CreateRoundRequest,
    ): RoundResponse {
        check()
        updated += roundId to request
        return respond(request).copy(roundId = roundId)
    }

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<RoundResponse> {
        check()
        pages += skip to take
        return Page(rounds.drop(skip).take(take), rounds.size)
    }

    override suspend fun round(roundId: UUID): RoundResponse? {
        check()
        return rounds.firstOrNull { it.roundId == roundId }
    }

    override suspend fun delete(roundId: UUID) {
        check()
        deleted += roundId
        rounds.removeAll { it.roundId == roundId }
    }

    override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? = score(roundId)

    override suspend fun microWins(roundId: UUID): List<MicroWinResponse> = microWins

    override suspend fun nextLever(roundId: UUID): NextLeverResponse? = Fixtures.nextLever(roundId)

    override suspend fun reflections(roundId: UUID): List<ReflectionResponse> = reflections

    val added = mutableListOf<Pair<UUID, CreateReflectionRequest>>()

    override suspend fun addReflection(
        roundId: UUID,
        request: CreateReflectionRequest,
    ): ReflectionResponse {
        check()
        added += roundId to request
        return Fixtures.reflection(roundId)
    }
}

/** The fakes the Hilt graph hands out; [reset] before each test. */
object FakeRepositories {
    lateinit var dashboard: FakeDashboardRepository
        private set
    lateinit var user: FakeUserRepository
        private set
    lateinit var rounds: FakeRoundsRepository
        private set
    lateinit var practice: FakePracticeRepository
        private set
    lateinit var progress: FakeProgressRepository
        private set

    fun reset() {
        dashboard = FakeDashboardRepository()
        user = FakeUserRepository()
        rounds = FakeRoundsRepository()
        practice = FakePracticeRepository()
        progress = FakeProgressRepository()
    }

    init {
        reset()
    }
}

@Module
@TestInstallIn(components = [SingletonComponent::class], replaces = [RepositoryModule::class])
object FakeRepositoryModule {
    @Provides
    fun dashboard(): DashboardRepository = FakeRepositories.dashboard

    @Provides
    fun user(): UserRepository = FakeRepositories.user

    @Provides
    fun rounds(): RoundsRepository = FakeRepositories.rounds

    @Provides
    fun practice(): PracticeRepository = FakeRepositories.practice

    @Provides
    fun progress(): ProgressRepository = FakeRepositories.progress
}

/** Practice sessions in memory, newest first; creating answers id 8. */
class FakePracticeRepository(
    sessions: List<PracticeSessionResponse> = emptyList(),
) : PracticeRepository {
    val sessions = sessions.toMutableList()
    val created = mutableListOf<CreatePracticeSessionRequest>()
    val deleted = mutableListOf<UUID>()
    var failWith: Exception? = null

    private fun check() = failWith?.let { throw it }

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<PracticeSessionResponse> {
        check()
        return Page(sessions.drop(skip).take(take), sessions.size)
    }

    override suspend fun session(id: UUID): PracticeSessionResponse? {
        check()
        return sessions.firstOrNull { it.practiceSessionId == id }
    }

    override suspend fun create(request: CreatePracticeSessionRequest): PracticeSessionResponse {
        check()
        created += request
        return Fixtures.practiceSession(8, request).also { sessions.add(0, it) }
    }

    override suspend fun delete(id: UUID) {
        check()
        deleted += id
        sessions.removeAll { it.practiceSessionId == id }
    }
}

/** Progress in memory: twelve months, a timeline, a baseline and reviews by month. */
class FakeProgressRepository(
    var roundCount: Int = 4,
    var timeline: List<RoundTimelinePointResponse> = Fixtures.timeline(),
    var months: List<MonthlyTimelinePointResponse> = Fixtures.months(),
    var baseline: UserBaselineResponse? = Fixtures.baseline(),
) : ProgressRepository {
    val reviews = mutableMapOf<String, MonthlyReviewResponse>()
    val created = mutableListOf<Pair<String, String?>>()
    val timelineQueries = mutableListOf<Pair<String?, String?>>()
    var failWith: Exception? = null

    private fun check() = failWith?.let { throw it }

    override suspend fun roundCount(): Int = roundCount.also { check() }

    override suspend fun timeline(
        from: String?,
        to: String?,
        take: Int,
    ): List<RoundTimelinePointResponse> {
        check()
        timelineQueries += from to to
        return timeline
    }

    override suspend fun monthly(months: Int): List<MonthlyTimelinePointResponse> {
        check()
        return this.months.map { it.copy(hasReview = it.monthStart in reviews) }
    }

    override suspend fun baseline(): UserBaselineResponse? = baseline.also { check() }

    override suspend fun review(monthStart: String): MonthlyReviewResponse? = reviews[monthStart].also { check() }

    override suspend fun createReview(
        monthStart: String,
        focusNextMonth: String?,
    ): MonthlyReviewResponse {
        check()
        created += monthStart to focusNextMonth
        return Fixtures.review(monthStart, focusNextMonth).also { reviews[monthStart] = it }
    }
}
