package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.DashboardRepository
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

/** Keeps what was sent; answers with [respond], by default the round as saved. */
class FakeRoundsRepository(
    var respond: (CreateRoundRequest) -> RoundResponse = { Fixtures.round(it) },
) : RoundsRepository {
    val created = mutableListOf<CreateRoundRequest>()
    val updated = mutableListOf<Pair<UUID, CreateRoundRequest>>()

    override suspend fun create(request: CreateRoundRequest): RoundResponse {
        created += request
        return respond(request)
    }

    override suspend fun update(
        roundId: UUID,
        request: CreateRoundRequest,
    ): RoundResponse {
        updated += roundId to request
        return respond(request).copy(roundId = roundId)
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
