package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.PracticeSessionsApi
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.totalCount
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** Practice sessions (#105): they count towards the Habits pillar. */
interface PracticeRepository {
    /** Newest first. */
    suspend fun page(
        skip: Int,
        take: Int,
    ): Page<PracticeSessionResponse>

    /** The session, or null when it no longer exists. */
    suspend fun session(id: UUID): PracticeSessionResponse?

    suspend fun create(request: CreatePracticeSessionRequest): PracticeSessionResponse

    suspend fun delete(id: UUID)
}

class ApiPracticeRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : PracticeRepository {
        override suspend fun page(
            skip: Int,
            take: Int,
        ): Page<PracticeSessionResponse> {
            val response = api.service<PracticeSessionsApi>().listMyPracticeSessions(skip = skip, take = take)
            return Page(response.unwrap(), response.totalCount())
        }

        override suspend fun session(id: UUID): PracticeSessionResponse? =
            nullWhenNotFound { api.service<PracticeSessionsApi>().getPracticeSession(id).unwrap() }

        override suspend fun create(request: CreatePracticeSessionRequest): PracticeSessionResponse =
            api.service<PracticeSessionsApi>().createPracticeSession(request).unwrap()

        override suspend fun delete(id: UUID) {
            api.service<PracticeSessionsApi>().deletePracticeSession(id).unwrap()
        }
    }
