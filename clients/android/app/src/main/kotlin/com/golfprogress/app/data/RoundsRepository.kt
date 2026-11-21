package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.CoachStudentDataApi
import com.golfprogress.api.generated.apis.MicroWinsApi
import com.golfprogress.api.generated.apis.ProgressApi
import com.golfprogress.api.generated.apis.RecommendationsApi
import com.golfprogress.api.generated.apis.ReflectionsApi
import com.golfprogress.api.generated.apis.RoundsApi
import com.golfprogress.api.generated.models.CreateReflectionRequest
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.totalCount
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** One page of a list, with the total from X-Total-Count. */
data class Page<T>(
    val items: List<T>,
    val total: Int?,
)

/**
 * Reading rounds: the signed-in golfer's own, or a student's for their coach (#208), with
 * the same contracts. A student's are read-only, so writing is only on [RoundsRepository].
 */
interface RoundReader {
    /** Newest first. */
    suspend fun page(
        skip: Int,
        take: Int,
    ): Page<RoundResponse>

    /** The round, or null when it no longer exists (deleted on another device), or can't be seen. */
    suspend fun round(roundId: UUID): RoundResponse?

    /** The round's score with its pillars and explanation, or null when it has none. */
    suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse?

    suspend fun microWins(roundId: UUID): List<MicroWinResponse>

    /** "Your next focus", read without recording a recommendation. */
    suspend fun nextLever(roundId: UUID): NextLeverResponse?

    /** Empty for a student who doesn't share reflections. */
    suspend fun reflections(roundId: UUID): List<ReflectionResponse>
}

/** Rounds: logging (#94), the list, detail, editing and deleting (#97). Logging scores it (#80). */
interface RoundsRepository : RoundReader {
    suspend fun create(request: CreateRoundRequest): RoundResponse

    suspend fun update(
        roundId: UUID,
        request: CreateRoundRequest,
    ): RoundResponse

    suspend fun delete(roundId: UUID)

    /** A student's rounds, read through the coach's routes (#208). */
    fun student(golferId: UUID): RoundReader

    /** Adds a reflection to the round (#101). */
    suspend fun addReflection(
        roundId: UUID,
        request: CreateReflectionRequest,
    ): ReflectionResponse
}

class ApiRoundsRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : RoundsRepository {
        override suspend fun create(request: CreateRoundRequest): RoundResponse = api.service<RoundsApi>().createRound(request).unwrap()

        override suspend fun update(
            roundId: UUID,
            request: CreateRoundRequest,
        ): RoundResponse = api.service<RoundsApi>().updateRound(roundId, request).unwrap()

        override suspend fun page(
            skip: Int,
            take: Int,
        ): Page<RoundResponse> {
            val response = api.service<RoundsApi>().listMyRounds(skip = skip, take = take)
            return Page(response.unwrap(), response.totalCount())
        }

        override suspend fun round(roundId: UUID): RoundResponse? = nullWhenNotFound { api.service<RoundsApi>().getRound(roundId).unwrap() }

        override suspend fun delete(roundId: UUID) {
            api.service<RoundsApi>().deleteRound(roundId).unwrap()
        }

        override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? =
            nullWhenNotFound { api.service<ProgressApi>().getFitnessScore(roundId).unwrap() }

        override suspend fun microWins(roundId: UUID): List<MicroWinResponse> =
            nullWhenNotFound { api.service<MicroWinsApi>().listMicroWins(roundId).unwrap() }.orEmpty()

        override suspend fun nextLever(roundId: UUID): NextLeverResponse? =
            nullWhenNotFound { api.service<RecommendationsApi>().getNextLever(roundId).unwrap() }

        override suspend fun reflections(roundId: UUID): List<ReflectionResponse> =
            nullWhenNotFound { api.service<ReflectionsApi>().listReflections(roundId).unwrap() }.orEmpty()

        override suspend fun addReflection(
            roundId: UUID,
            request: CreateReflectionRequest,
        ): ReflectionResponse = api.service<ReflectionsApi>().createReflection(roundId, request).unwrap()

        override fun student(golferId: UUID): RoundReader = StudentRounds(api, golferId)
    }

/** A student's rounds through /coach/students/{golferId}/…: a 404 there is "not shared" or "gone". */
private class StudentRounds(
    private val api: GolfProgressApi,
    private val golferId: UUID,
) : RoundReader {
    private val service get() = api.service<CoachStudentDataApi>()

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<RoundResponse> {
        val response = service.listStudentRounds(golferId, skip = skip, take = take)
        return Page(response.unwrap(), response.totalCount())
    }

    override suspend fun round(roundId: UUID): RoundResponse? = nullWhenNotFound { service.getStudentRound(golferId, roundId).unwrap() }

    override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? =
        nullWhenNotFound { service.getStudentFitnessScore(golferId, roundId).unwrap() }

    override suspend fun microWins(roundId: UUID): List<MicroWinResponse> =
        nullWhenNotFound { service.listStudentMicroWins(golferId, roundId).unwrap() }.orEmpty()

    override suspend fun nextLever(roundId: UUID): NextLeverResponse? =
        nullWhenNotFound { service.getStudentNextLever(golferId, roundId).unwrap() }

    override suspend fun reflections(roundId: UUID): List<ReflectionResponse> =
        nullWhenNotFound { service.listStudentReflections(golferId, roundId).unwrap() }.orEmpty()
}
