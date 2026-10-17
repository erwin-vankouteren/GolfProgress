package com.golfprogress.app.data

import com.golfprogress.api.ApiProblem
import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.DashboardApi
import com.golfprogress.api.generated.apis.ProgressApi
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/**
 * Home (#90): where the user stands after their last round. Repositories are the layer
 * between view models and the generated client (docs/architecture.md → "Client
 * architecture"); tests replace them with fakes.
 */
interface DashboardRepository {
    suspend fun dashboard(): DashboardResponse

    /** A round's score with its pillars, or null when it hasn't been scored. */
    suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse?
}

class ApiDashboardRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : DashboardRepository {
        override suspend fun dashboard(): DashboardResponse = api.service<DashboardApi>().getDashboard().unwrap()

        override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? =
            nullWhenNotFound { api.service<ProgressApi>().getFitnessScore(roundId).unwrap() }
    }

/** A 404 means "there's none", not a failure. */
suspend fun <T> nullWhenNotFound(load: suspend () -> T): T? =
    try {
        load()
    } catch (e: ApiProblem) {
        if (e.status == 404) null else throw e
    }
