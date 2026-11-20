package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.FeedbackApi
import com.golfprogress.api.generated.models.CreateFeedbackRequest
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import com.golfprogress.api.generated.models.MarkFeedbackReadRequest
import com.golfprogress.api.generated.models.UnreadFeedbackResponse
import com.golfprogress.api.generated.models.UpdateFeedbackRequest
import com.golfprogress.api.unwrap
import com.golfprogress.app.feedback.FeedbackTarget
import java.time.OffsetDateTime
import java.util.UUID
import javax.inject.Inject

/** Feedback threads on the golfer's rounds, reflections and practice sessions (#217). */
interface FeedbackRepository {
    /** Every coach's thread on the item, oldest first. */
    suspend fun thread(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
    ): List<FeedbackMessageResponse>

    /** In [coachId]'s thread. A 400 on coachId when they can't see it; 429 after 60 an hour. */
    suspend fun write(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
        coachId: UUID,
        text: String,
    ): FeedbackMessageResponse

    suspend fun edit(
        id: UUID,
        text: String,
    ): FeedbackMessageResponse

    suspend fun delete(id: UUID)

    /** Marks the feedback on this item read; other items stay unread. */
    suspend fun markRead(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
        upTo: OffsetDateTime,
    )

    /** "New from your coach" (#222): unread messages from coaches, and the newest threads. */
    suspend fun unread(): UnreadFeedbackResponse
}

class ApiFeedbackRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : FeedbackRepository {
        override suspend fun thread(
            golferId: UUID,
            target: FeedbackTarget,
            targetId: UUID,
        ): List<FeedbackMessageResponse> =
            api
                .service<FeedbackApi>()
                .getFeedbackThread(golferId, FeedbackApi.TargetTypeGetFeedbackThread.valueOf(target.name), targetId)
                .unwrap()

        override suspend fun write(
            golferId: UUID,
            target: FeedbackTarget,
            targetId: UUID,
            coachId: UUID,
            text: String,
        ): FeedbackMessageResponse =
            api
                .service<FeedbackApi>()
                .writeFeedback(
                    CreateFeedbackRequest(golferId, CreateFeedbackRequest.TargetType.valueOf(target.name), targetId, text, coachId),
                ).unwrap()

        override suspend fun edit(
            id: UUID,
            text: String,
        ): FeedbackMessageResponse = api.service<FeedbackApi>().editFeedback(id, UpdateFeedbackRequest(text)).unwrap()

        override suspend fun delete(id: UUID) {
            api.service<FeedbackApi>().deleteFeedback(id).unwrap()
        }

        override suspend fun markRead(
            golferId: UUID,
            target: FeedbackTarget,
            targetId: UUID,
            upTo: OffsetDateTime,
        ) {
            api
                .service<FeedbackApi>()
                .markFeedbackRead(
                    MarkFeedbackReadRequest(golferId, MarkFeedbackReadRequest.TargetType.valueOf(target.name), targetId, upTo),
                ).unwrap()
        }

        override suspend fun unread(): UnreadFeedbackResponse = api.service<FeedbackApi>().getUnreadFeedback().unwrap()
    }
