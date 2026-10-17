package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.RoundsApi
import com.golfprogress.api.generated.apis.UsersApi
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** Logging and editing rounds (#94, #97). Logging a round scores it (#80). */
interface RoundsRepository {
    suspend fun create(request: CreateRoundRequest): RoundResponse

    suspend fun update(
        roundId: UUID,
        request: CreateRoundRequest,
    ): RoundResponse
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
    }

/** The signed-in user's profile and preferences, e.g. the tracking mode Log round follows. */
interface UserRepository {
    suspend fun me(): UserResponse
}

class ApiUserRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : UserRepository {
        override suspend fun me(): UserResponse = api.service<UsersApi>().getMe().unwrap()
    }
