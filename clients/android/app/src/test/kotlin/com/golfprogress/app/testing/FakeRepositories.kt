package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.CreateReflectionRequest
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.data.Page
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

    fun reset() {
        dashboard = FakeDashboardRepository()
        user = FakeUserRepository()
        rounds = FakeRoundsRepository()
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
}
