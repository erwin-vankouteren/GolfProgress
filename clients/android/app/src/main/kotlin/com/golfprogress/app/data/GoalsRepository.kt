package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.PracticeGoalsApi
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalRequest
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.api.generated.models.UpdatePracticeGoalStatusRequest
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** Reading goals: the golfer's from every coach, or the ones a coach set for a student (#208). */
interface GoalReader {
    /** Current ones first. */
    suspend fun goals(): List<PracticeGoalResponse>

    /** A goal with its linked sessions, or null when it no longer exists, or can't be seen. */
    suspend fun goal(id: UUID): PracticeGoalDetailResponse?

    /** "Did it work?" */
    suspend fun outcome(id: UUID): GoalOutcomeResponse
}

/**
 * The goals a coach sets for a student (#208), through /coach/students/{golferId}/practice-goals:
 * only their own, and only while the student shares practice.
 */
interface StudentGoals : GoalReader {
    /** A 400 names the field that's wrong. */
    suspend fun set(request: PracticeGoalRequest): PracticeGoalResponse

    /** Changes the whole goal. */
    suspend fun change(
        id: UUID,
        request: PracticeGoalRequest,
    ): PracticeGoalResponse

    /** A 409 once a session is linked to it. */
    suspend fun remove(id: UUID)
}

/** Practice goals from the golfer's coaches (#231, #236). */
interface GoalsRepository : GoalReader {
    /** Marks it done ([done]) or dismisses it. */
    suspend fun setStatus(
        id: UUID,
        done: Boolean,
    ): PracticeGoalResponse

    /** The goals the signed-in coach set for a student (#208). */
    fun student(golferId: UUID): StudentGoals
}

class ApiGoalsRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : GoalsRepository {
        override suspend fun goals(): List<PracticeGoalResponse> = api.service<PracticeGoalsApi>().listMyPracticeGoals().unwrap()

        override suspend fun goal(id: UUID): PracticeGoalDetailResponse? =
            nullWhenNotFound { api.service<PracticeGoalsApi>().getMyPracticeGoal(id).unwrap() }

        override suspend fun setStatus(
            id: UUID,
            done: Boolean,
        ): PracticeGoalResponse =
            api
                .service<PracticeGoalsApi>()
                .updateMyPracticeGoal(
                    id,
                    UpdatePracticeGoalStatusRequest(
                        if (done) UpdatePracticeGoalStatusRequest.Status.Done else UpdatePracticeGoalStatusRequest.Status.Dismissed,
                    ),
                ).unwrap()

        override suspend fun outcome(id: UUID): GoalOutcomeResponse = api.service<PracticeGoalsApi>().getMyPracticeGoalOutcome(id).unwrap()

        override fun student(golferId: UUID): StudentGoals = ApiStudentGoals(api, golferId)
    }

private class ApiStudentGoals(
    private val api: GolfProgressApi,
    private val golferId: UUID,
) : StudentGoals {
    private val service get() = api.service<PracticeGoalsApi>()

    override suspend fun goals(): List<PracticeGoalResponse> = service.listStudentPracticeGoals(golferId).unwrap()

    override suspend fun goal(id: UUID): PracticeGoalDetailResponse? =
        nullWhenNotFound { service.getStudentPracticeGoal(golferId, id).unwrap() }

    override suspend fun outcome(id: UUID): GoalOutcomeResponse = service.getStudentPracticeGoalOutcome(golferId, id).unwrap()

    override suspend fun set(request: PracticeGoalRequest): PracticeGoalResponse = service.setPracticeGoal(golferId, request).unwrap()

    override suspend fun change(
        id: UUID,
        request: PracticeGoalRequest,
    ): PracticeGoalResponse = service.changePracticeGoal(golferId, id, request).unwrap()

    override suspend fun remove(id: UUID) {
        service.removePracticeGoal(golferId, id).unwrap()
    }
}
