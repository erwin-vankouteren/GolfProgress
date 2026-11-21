package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.CoachStudentDataApi
import com.golfprogress.api.generated.apis.PracticeSessionsApi
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.totalCount
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** Reading practice sessions: the golfer's own, or a student's for their coach (#208). */
interface PracticeReader {
    /** Newest first. */
    suspend fun page(
        skip: Int,
        take: Int,
    ): Page<PracticeSessionResponse>

    /** The session, or null when it no longer exists, or can't be seen. */
    suspend fun session(id: UUID): PracticeSessionResponse?
}

/** Practice sessions (#105): they count towards the Habits pillar. */
interface PracticeRepository : PracticeReader {
    suspend fun create(request: CreatePracticeSessionRequest): PracticeSessionResponse

    suspend fun delete(id: UUID)

    /** A student's sessions, read through the coach's routes (#208): only while they share practice. */
    fun student(golferId: UUID): PracticeReader
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

        override fun student(golferId: UUID): PracticeReader = StudentPractice(api, golferId)
    }

/** A student's sessions through /coach/students/{golferId}/…: a 404 there is "not shared" or "gone". */
private class StudentPractice(
    private val api: GolfProgressApi,
    private val golferId: UUID,
) : PracticeReader {
    private val service get() = api.service<CoachStudentDataApi>()

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<PracticeSessionResponse> {
        val response = service.listStudentPracticeSessions(golferId, skip = skip, take = take)
        return Page(response.unwrap(), response.totalCount())
    }

    override suspend fun session(id: UUID): PracticeSessionResponse? =
        nullWhenNotFound { service.getStudentPracticeSession(golferId, id).unwrap() }
}
