package com.golfprogress.app.testing

import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.CoachInvitationResponse
import com.golfprogress.api.generated.models.CoachResponse
import com.golfprogress.api.generated.models.CoachesResponse
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.CreateReflectionRequest
import com.golfprogress.api.generated.models.CreateRoundRequest
import com.golfprogress.api.generated.models.DashboardResponse
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.MicroWinResponse
import com.golfprogress.api.generated.models.MonthlyReviewResponse
import com.golfprogress.api.generated.models.MonthlyTimelinePointResponse
import com.golfprogress.api.generated.models.NextLeverResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.generated.models.ReflectionResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.RoundTimelinePointResponse
import com.golfprogress.api.generated.models.UnreadFeedbackResponse
import com.golfprogress.api.generated.models.UserBaselineResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.CoachingRepository
import com.golfprogress.app.data.DashboardRepository
import com.golfprogress.app.data.FeedbackRepository
import com.golfprogress.app.data.GoalsRepository
import com.golfprogress.app.data.Page
import com.golfprogress.app.data.PracticeRepository
import com.golfprogress.app.data.ProfileChanges
import com.golfprogress.app.data.ProgressRepository
import com.golfprogress.app.data.RoundsRepository
import com.golfprogress.app.data.UserRepository
import com.golfprogress.app.di.RepositoryModule
import com.golfprogress.app.feedback.FeedbackTarget
import dagger.Module
import dagger.Provides
import dagger.hilt.components.SingletonComponent
import dagger.hilt.testing.TestInstallIn
import java.time.OffsetDateTime
import java.util.UUID

/*
 * In-memory repositories for tests. View-model tests construct them directly; Hilt UI
 * tests get them in place of the real ones (FakeRepositoryModule) and set them up
 * through FakeRepositories before showing a screen.
 */

class FakeDashboardRepository(
    var dashboard: () -> DashboardResponse = { Fixtures.dashboard() },
    var scores: Map<UUID, FitnessScoreResponse> = mapOf(Fixtures.roundId(1) to Fixtures.fitnessScore()),
) : DashboardRepository {
    var dashboardCalls = 0
        private set

    override suspend fun dashboard(): DashboardResponse {
        dashboardCalls++
        return dashboard.invoke()
    }

    override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? = scores[roundId]
}

/**
 * Coaches and invitations in memory. [failWith] makes inviting and cancelling throw, and
 * [listFailsWith] the list.
 */
class FakeCoachingRepository(
    var coaches: List<CoachResponse> = emptyList(),
    val invitations: MutableList<CoachInvitationResponse> = mutableListOf(),
    var listFailsWith: Exception? = null,
) : CoachingRepository {
    val invited = mutableListOf<Triple<String, Boolean, Boolean>>()
    var failWith: Exception? = null

    override suspend fun coaches(): CoachesResponse {
        listFailsWith?.let { throw it }
        return CoachesResponse(coaches, invitations.toList())
    }

    override suspend fun invite(
        email: String,
        shareReflections: Boolean,
        sharePractice: Boolean,
    ): CoachInvitationResponse {
        failWith?.let { throw it }
        invited += Triple(email, shareReflections, sharePractice)
        val invitation = Fixtures.coachInvitation(email, shareReflections, sharePractice)
        invitations += invitation
        return invitation
    }

    override suspend fun cancelInvitation(id: UUID) {
        failWith?.let { throw it }
        invitations.removeAll { it.coachInvitationId == id }
    }

    override suspend fun changeSharing(
        linkId: UUID,
        shareReflections: Boolean,
        sharePractice: Boolean,
    ): CoachResponse {
        failWith?.let { throw it }
        val changed = coaches.first { it.coachLinkId == linkId }.copy(shareReflections = shareReflections, sharePractice = sharePractice)
        coaches = coaches.map { if (it.coachLinkId == linkId) changed else it }
        return changed
    }

    override suspend fun removeCoach(linkId: UUID) {
        failWith?.let { throw it }
        coaches = coaches.filterNot { it.coachLinkId == linkId }
    }
}

/**
 * The profile in memory: updates apply as the API applies them, [password] is the
 * account's password, and [failWith] makes the account calls throw.
 */
class FakeUserRepository(
    var me: () -> UserResponse = { Fixtures.user() },
    var password: String = "correct horse",
) : UserRepository {
    val updates = mutableListOf<ProfileChanges>()
    val passwordChanges = mutableListOf<Pair<String, String>>()
    var deleted = false
    var failWith: Exception? = null

    override suspend fun me(): UserResponse = me.invoke()

    override suspend fun update(changes: ProfileChanges): UserResponse {
        failWith?.let { throw it }
        updates += changes
        val current = me()
        val updated =
            current.copy(
                displayName = changes.displayName ?: current.displayName,
                units = changes.units?.let { u -> UserResponse.Units.entries.first { it.value == u } } ?: current.units,
                trackingMode =
                    changes.trackingMode?.let { t -> UserResponse.TrackingMode.entries.first { it.value == t } } ?: current.trackingMode,
                goalFocus = changes.goalFocus ?: current.goalFocus,
                handicapIndex = changes.handicapIndex?.let { it.value } ?: current.handicapIndex,
                typical18Score = changes.typical18Score?.let { it.value } ?: current.typical18Score,
            )
        me = { updated }
        return updated
    }

    override suspend fun changePassword(
        current: String,
        new: String,
    ) {
        failWith?.let { throw it }
        if (current != password) throw ApiProblem(400, errors = mapOf("CurrentPassword" to listOf("The current password is incorrect.")))
        passwordChanges += current to new
        password = new
    }

    override suspend fun deleteAccount(password: String) {
        failWith?.let { throw it }
        if (password != this.password) throw ApiProblem(400, errors = mapOf("Password" to listOf("The password is incorrect.")))
        deleted = true
    }
}

/**
 * Rounds in memory, newest first. Creating answers with [respond] (by default the round
 * as saved, id 9); the rest read and change [rounds]. [failWith] makes every call throw.
 */
class FakeRoundsRepository(
    rounds: List<RoundResponse> = emptyList(),
    var respond: (CreateRoundRequest) -> RoundResponse = { Fixtures.round(it) },
) : RoundsRepository {
    val rounds = rounds.toMutableList()
    val created = mutableListOf<CreateRoundRequest>()
    val updated = mutableListOf<Pair<UUID, CreateRoundRequest>>()
    val deleted = mutableListOf<UUID>()
    val pages = mutableListOf<Pair<Int, Int>>()
    var failWith: Exception? = null
    var score: (UUID) -> FitnessScoreResponse? = { Fixtures.fitnessScore(it) }
    var microWins: List<MicroWinResponse> = listOf(Fixtures.microWin("Fewer penalties"))
    var reflections: List<ReflectionResponse> = emptyList()

    private fun check() = failWith?.let { throw it }

    override suspend fun create(request: CreateRoundRequest): RoundResponse {
        check()
        created += request
        return respond(request).also { rounds.add(0, it) }
    }

    override suspend fun update(
        roundId: UUID,
        request: CreateRoundRequest,
    ): RoundResponse {
        check()
        updated += roundId to request
        return respond(request).copy(roundId = roundId)
    }

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<RoundResponse> {
        check()
        pages += skip to take
        return Page(rounds.drop(skip).take(take), rounds.size)
    }

    override suspend fun round(roundId: UUID): RoundResponse? {
        check()
        return rounds.firstOrNull { it.roundId == roundId }
    }

    override suspend fun delete(roundId: UUID) {
        check()
        deleted += roundId
        rounds.removeAll { it.roundId == roundId }
    }

    override suspend fun fitnessScore(roundId: UUID): FitnessScoreResponse? = score(roundId)

    override suspend fun microWins(roundId: UUID): List<MicroWinResponse> = microWins

    override suspend fun nextLever(roundId: UUID): NextLeverResponse? = Fixtures.nextLever(roundId)

    override suspend fun reflections(roundId: UUID): List<ReflectionResponse> = reflections

    val added = mutableListOf<Pair<UUID, CreateReflectionRequest>>()

    override suspend fun addReflection(
        roundId: UUID,
        request: CreateReflectionRequest,
    ): ReflectionResponse {
        check()
        added += roundId to request
        return Fixtures.reflection(roundId)
    }
}

/**
 * Feedback threads in memory, answering as the golfer: writing adds a message of theirs,
 * editing and deleting change it, and [reads] records marking read. [failWith] makes the
 * writes throw, and [threadFailsWith] reading.
 */
class FakeFeedbackRepository(
    val messages: MutableList<FeedbackMessageResponse> = mutableListOf(),
    var threadFailsWith: Exception? = null,
) : FeedbackRepository {
    val written = mutableListOf<Pair<UUID, String>>()
    val reads = mutableListOf<Pair<UUID, OffsetDateTime>>()
    var failWith: Exception? = null

    /** What GET /me/feedback/unread answers; [unreadFailsWith] makes it throw. */
    var unread: UnreadFeedbackResponse = UnreadFeedbackResponse(0, emptyList())
    var unreadFailsWith: Exception? = null

    override suspend fun thread(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
    ): List<FeedbackMessageResponse> {
        threadFailsWith?.let { throw it }
        return messages.toList()
    }

    override suspend fun write(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
        coachId: UUID,
        text: String,
    ): FeedbackMessageResponse {
        failWith?.let { throw it }
        written += coachId to text
        val message = Fixtures.feedback(100 + messages.size, text, coachId, fromCoach = false)
        messages += message
        return message
    }

    override suspend fun edit(
        id: UUID,
        text: String,
    ): FeedbackMessageResponse {
        failWith?.let { throw it }
        val edited = messages.first { it.feedbackMessageId == id }.copy(text = text, editedAt = Fixtures.now)
        messages.replaceAll { if (it.feedbackMessageId == id) edited else it }
        return edited
    }

    override suspend fun delete(id: UUID) {
        failWith?.let { throw it }
        messages.removeAll { it.feedbackMessageId == id }
    }

    override suspend fun markRead(
        golferId: UUID,
        target: FeedbackTarget,
        targetId: UUID,
        upTo: OffsetDateTime,
    ) {
        reads += targetId to upTo
    }

    override suspend fun unread(): UnreadFeedbackResponse {
        unreadFailsWith?.let { throw it }
        return unread
    }
}

/**
 * Goals in memory: setting a status changes the goal, and [details] and [outcomes] answer
 * the goal page. [failWith] makes setting a status throw.
 */
class FakeGoalsRepository(
    var goals: List<PracticeGoalResponse> = emptyList(),
    val details: MutableMap<UUID, PracticeGoalDetailResponse> = mutableMapOf(),
    val outcomes: MutableMap<UUID, GoalOutcomeResponse> = mutableMapOf(),
) : GoalsRepository {
    val statusChanges = mutableListOf<Pair<UUID, Boolean>>()
    var failWith: Exception? = null

    override suspend fun goals(): List<PracticeGoalResponse> = goals

    override suspend fun goal(id: UUID): PracticeGoalDetailResponse? = details[id]

    override suspend fun setStatus(
        id: UUID,
        done: Boolean,
    ): PracticeGoalResponse {
        failWith?.let { throw it }
        statusChanges += id to done
        val status = if (done) PracticeGoalResponse.Status.Done else PracticeGoalResponse.Status.Dismissed
        val changed = (details[id]?.goal ?: goals.first { it.practiceGoalId == id }).copy(status = status)
        details[id]?.let { details[id] = it.copy(goal = changed) }
        goals = goals.map { if (it.practiceGoalId == id) changed else it }
        return changed
    }

    override suspend fun outcome(id: UUID): GoalOutcomeResponse = outcomes[id] ?: throw java.io.IOException()
}

/** The fakes the Hilt graph hands out; [reset] before each test. */
object FakeRepositories {
    lateinit var dashboard: FakeDashboardRepository
        private set
    lateinit var user: FakeUserRepository
        private set
    lateinit var rounds: FakeRoundsRepository
        private set
    lateinit var practice: FakePracticeRepository
        private set
    lateinit var progress: FakeProgressRepository
        private set
    lateinit var coaching: FakeCoachingRepository
        private set
    lateinit var feedback: FakeFeedbackRepository
        private set
    lateinit var goals: FakeGoalsRepository
        private set

    fun reset() {
        dashboard = FakeDashboardRepository()
        user = FakeUserRepository()
        rounds = FakeRoundsRepository()
        practice = FakePracticeRepository()
        progress = FakeProgressRepository()
        coaching = FakeCoachingRepository()
        feedback = FakeFeedbackRepository()
        goals = FakeGoalsRepository()
    }

    init {
        reset()
    }
}

@Module
@TestInstallIn(components = [SingletonComponent::class], replaces = [RepositoryModule::class])
object FakeRepositoryModule {
    @Provides
    fun dashboard(): DashboardRepository = FakeRepositories.dashboard

    @Provides
    fun user(): UserRepository = FakeRepositories.user

    @Provides
    fun rounds(): RoundsRepository = FakeRepositories.rounds

    @Provides
    fun practice(): PracticeRepository = FakeRepositories.practice

    @Provides
    fun progress(): ProgressRepository = FakeRepositories.progress

    @Provides
    fun coaching(): CoachingRepository = FakeRepositories.coaching

    @Provides
    fun feedback(): FeedbackRepository = FakeRepositories.feedback

    @Provides
    fun goals(): GoalsRepository = FakeRepositories.goals
}

/** Practice sessions in memory, newest first; creating answers id 8. */
class FakePracticeRepository(
    sessions: List<PracticeSessionResponse> = emptyList(),
) : PracticeRepository {
    val sessions = sessions.toMutableList()
    val created = mutableListOf<CreatePracticeSessionRequest>()
    val deleted = mutableListOf<UUID>()
    var failWith: Exception? = null

    private fun check() = failWith?.let { throw it }

    override suspend fun page(
        skip: Int,
        take: Int,
    ): Page<PracticeSessionResponse> {
        check()
        return Page(sessions.drop(skip).take(take), sessions.size)
    }

    override suspend fun session(id: UUID): PracticeSessionResponse? {
        check()
        return sessions.firstOrNull { it.practiceSessionId == id }
    }

    override suspend fun create(request: CreatePracticeSessionRequest): PracticeSessionResponse {
        check()
        created += request
        return Fixtures.practiceSession(8, request).also { sessions.add(0, it) }
    }

    override suspend fun delete(id: UUID) {
        check()
        deleted += id
        sessions.removeAll { it.practiceSessionId == id }
    }
}

/** Progress in memory: twelve months, a timeline, a baseline and reviews by month. */
class FakeProgressRepository(
    var roundCount: Int = 4,
    var timeline: List<RoundTimelinePointResponse> = Fixtures.timeline(),
    var months: List<MonthlyTimelinePointResponse> = Fixtures.months(),
    var baseline: UserBaselineResponse? = Fixtures.baseline(),
) : ProgressRepository {
    val reviews = mutableMapOf<String, MonthlyReviewResponse>()
    val created = mutableListOf<Pair<String, String?>>()
    val timelineQueries = mutableListOf<Pair<String?, String?>>()
    var failWith: Exception? = null

    private fun check() = failWith?.let { throw it }

    override suspend fun roundCount(): Int = roundCount.also { check() }

    override suspend fun timeline(
        from: String?,
        to: String?,
        take: Int,
    ): List<RoundTimelinePointResponse> {
        check()
        timelineQueries += from to to
        return timeline
    }

    override suspend fun monthly(months: Int): List<MonthlyTimelinePointResponse> {
        check()
        return this.months.map { it.copy(hasReview = it.monthStart in reviews) }
    }

    override suspend fun baseline(): UserBaselineResponse? = baseline.also { check() }

    override suspend fun review(monthStart: String): MonthlyReviewResponse? = reviews[monthStart].also { check() }

    override suspend fun createReview(
        monthStart: String,
        focusNextMonth: String?,
    ): MonthlyReviewResponse {
        check()
        created += monthStart to focusNextMonth
        return Fixtures.review(monthStart, focusNextMonth).also { reviews[monthStart] = it }
    }
}
