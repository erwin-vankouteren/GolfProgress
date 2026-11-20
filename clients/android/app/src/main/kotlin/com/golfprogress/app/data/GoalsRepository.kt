package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.PracticeGoalsApi
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.api.generated.models.UpdatePracticeGoalStatusRequest
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** Practice goals from the golfer's coaches (#231, #236). */
interface GoalsRepository {
    /** Every coach's goals, current ones first. */
    suspend fun goals(): List<PracticeGoalResponse>

    /** A goal with its linked sessions, or null when it no longer exists. */
    suspend fun goal(id: UUID): PracticeGoalDetailResponse?

    /** Marks it done ([done]) or dismisses it. */
    suspend fun setStatus(
        id: UUID,
        done: Boolean,
    ): PracticeGoalResponse

    /** "Did it work?" */
    suspend fun outcome(id: UUID): GoalOutcomeResponse
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
    }
