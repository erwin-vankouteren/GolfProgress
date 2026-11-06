package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.CoachInvitationsApi
import com.golfprogress.api.generated.models.CoachInvitationResponse
import com.golfprogress.api.generated.models.CoachesResponse
import com.golfprogress.api.generated.models.CreateCoachInvitationRequest
import com.golfprogress.api.unwrap
import java.util.UUID
import javax.inject.Inject

/** The golfer's coaches and invitations (#186). Accepting happens on the web. */
interface CoachingRepository {
    suspend fun coaches(): CoachesResponse

    /** A refused address is a 400 on email; too many invitations today is a 429. */
    suspend fun invite(
        email: String,
        shareReflections: Boolean,
        sharePractice: Boolean,
    ): CoachInvitationResponse

    /** A 404 when it's no longer pending. */
    suspend fun cancelInvitation(id: UUID)
}

class ApiCoachingRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : CoachingRepository {
        override suspend fun coaches(): CoachesResponse = api.service<CoachInvitationsApi>().listCoaches().unwrap()

        override suspend fun invite(
            email: String,
            shareReflections: Boolean,
            sharePractice: Boolean,
        ): CoachInvitationResponse =
            api
                .service<CoachInvitationsApi>()
                .inviteCoach(CreateCoachInvitationRequest(email, shareReflections, sharePractice))
                .unwrap()

        override suspend fun cancelInvitation(id: UUID) {
            api.service<CoachInvitationsApi>().cancelCoachInvitation(id).unwrap()
        }
    }
