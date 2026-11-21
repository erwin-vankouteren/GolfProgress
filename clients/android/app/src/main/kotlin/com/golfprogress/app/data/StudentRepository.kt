package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.CoachStudentsApi
import com.golfprogress.api.generated.models.StudentOverviewResponse
import com.golfprogress.api.generated.models.StudentResponse
import com.golfprogress.api.totalCount
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/**
 * The golfers a coach coaches (#208, docs/coach-access.md → "Screens"). Their rounds,
 * practice and goals are read through [RoundsRepository.student], [PracticeRepository.student]
 * and [GoalsRepository.student].
 */
interface StudentRepository {
    /** Newest activity first. */
    suspend fun students(
        skip: Int,
        take: Int,
    ): Page<StudentResponse>

    /** What a coach needs before a lesson; null once they no longer coach them. */
    suspend fun overview(golferId: UUID): StudentOverviewResponse?

    /** Ends the coaching at once. A 404 when the golfer removed them already. */
    suspend fun stopCoaching(golferId: UUID)
}

class ApiStudentRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : StudentRepository {
        override suspend fun students(
            skip: Int,
            take: Int,
        ): Page<StudentResponse> {
            val response = api.service<CoachStudentsApi>().listStudents(skip = skip, take = take)
            return Page(response.unwrap(), response.totalCount())
        }

        override suspend fun overview(golferId: UUID): StudentOverviewResponse? =
            nullWhenNotFound { api.service<CoachStudentsApi>().getStudentOverview(golferId).unwrap() }

        override suspend fun stopCoaching(golferId: UUID) {
            api.service<CoachStudentsApi>().stopCoaching(golferId).unwrap()
        }
    }
