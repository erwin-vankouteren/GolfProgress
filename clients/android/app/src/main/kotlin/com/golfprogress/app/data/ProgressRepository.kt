package com.golfprogress.app.data

import com.golfprogress.api.GolfProgressApi
import com.golfprogress.api.generated.apis.MonthlyReviewsApi
import com.golfprogress.api.generated.apis.ProgressApi
import com.golfprogress.api.generated.apis.RoundsApi
import com.golfprogress.api.generated.apis.TimelineApi
import com.golfprogress.api.generated.models.GenerateMonthlyReviewRequest
import com.golfprogress.api.generated.models.MonthlyReviewResponse
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.UserBaselineResponse
import com.golfprogress.api.totalCount
import com.golfprogress.api.unwrap
import javax.inject.Inject

/** Progress (#109): the timeline, monthly trend, baseline and monthly reviews. */
interface ProgressRepository {
    /** How many rounds the user has logged, from X-Total-Count. */
    suspend fun roundCount(): Int

    /** Rounds in a date range (yyyy-MM-dd, either end open), oldest first. */
    suspend fun timeline(
        from: String?,
        to: String?,
        take: Int,
    ): List<RoundTimelinePointResponse>

    /** One point per calendar month for the last [months] months, oldest first. */
    suspend fun monthly(months: Int): List<MonthlyTimelinePointResponse>

    /** The current baseline, or null before the first round. */
    suspend fun baseline(): UserBaselineResponse?

    /** A month's review, or null when none was created for it. */
    suspend fun review(monthStart: String): MonthlyReviewResponse?

    suspend fun createReview(
        monthStart: String,
        focusNextMonth: String?,
    ): MonthlyReviewResponse
}

class ApiProgressRepository
    @Inject
    constructor(
        private val api: GolfProgressApi,
    ) : ProgressRepository {
        override suspend fun roundCount(): Int {
            val response = api.service<RoundsApi>().listMyRounds(skip = 0, take = 1)
            return response.totalCount() ?: response.unwrap().size
        }

        override suspend fun timeline(
            from: String?,
            to: String?,
            take: Int,
        ): List<RoundTimelinePointResponse> =
            api
                .service<TimelineApi>()
                .getTimeline(take = take, from = from, to = to)
                .unwrap()
                .items

        override suspend fun monthly(months: Int): List<MonthlyTimelinePointResponse> =
            api
                .service<TimelineApi>()
                .getMonthlyTimeline(months)
                .unwrap()
                .items

        override suspend fun baseline(): UserBaselineResponse? = nullWhenNotFound { api.service<ProgressApi>().getMyBaseline().unwrap() }

        override suspend fun review(monthStart: String): MonthlyReviewResponse? =
            nullWhenNotFound { api.service<MonthlyReviewsApi>().getMonthlyReview(monthStart).unwrap() }

        override suspend fun createReview(
            monthStart: String,
            focusNextMonth: String?,
        ): MonthlyReviewResponse =
            api
                .service<MonthlyReviewsApi>()
                .generateMonthlyReview(GenerateMonthlyReviewRequest(monthStart, focusNextMonth))
                .unwrap()
    }
