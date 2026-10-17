package com.golfprogress.app.testing

import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.di.RepositoryModule
import dagger.Module
import dagger.Provides
import dagger.hilt.components.SingletonComponent
import dagger.hilt.testing.TestInstallIn
import java.util.UUID

/**
 * In-memory repositories for tests. View-model tests construct them directly; Hilt UI
 * tests get them in place of the real ones ([FakeRepositoryModule]) and set them up
 * through [FakeRepositories] before showing a screen.
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

/** The fakes the Hilt graph hands out; reset by [reset] before each test. */
object FakeRepositories {
    lateinit var dashboard: FakeDashboardRepository
        private set

    fun reset() {
        dashboard = FakeDashboardRepository()
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
}
