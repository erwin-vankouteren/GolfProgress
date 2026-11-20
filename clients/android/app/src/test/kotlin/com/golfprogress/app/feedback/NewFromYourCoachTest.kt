package com.golfprogress.app.feedback

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import com.golfprogress.api.generated.models.UnreadFeedbackResponse
import com.golfprogress.api.generated.models.UnreadFeedbackThreadResponse
import com.golfprogress.app.dashboard.DashboardContent
import com.golfprogress.app.dashboard.DashboardState
import com.golfprogress.app.dashboard.DashboardViewModel
import com.golfprogress.app.testing.FakeDashboardRepository
import com.golfprogress.app.testing.FakeFeedbackRepository
import com.golfprogress.app.testing.Fixtures
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.theme.GolfProgressTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException
import java.util.UUID

private val sessionId: UUID = UUID(2, 1)

private fun thread(
    type: UnreadFeedbackThreadResponse.TargetType = UnreadFeedbackThreadResponse.TargetType.Round,
    targetId: UUID = Fixtures.roundId(1),
    roundId: UUID? = Fixtures.roundId(1),
    coachName: String? = "Pro",
    unread: Int = 2,
) = UnreadFeedbackThreadResponse(
    targetType = type,
    targetId = targetId,
    roundId = roundId,
    onDate = "2026-11-01",
    coachId = UUID(1, 1),
    coachName = coachName,
    unread = unread,
    newestAt = Fixtures.now,
    preview = "Lag putts to a metre.",
)

/** The Home card's rules (#222): the web app's unread.ts. */
class UnreadRulesTest {
    @Test
    fun `opens the round, a reflection's round, or the session`() {
        assertEquals(UnreadDestination.Round(Fixtures.roundId(1)), destinationOf(thread()))
        assertEquals(
            UnreadDestination.Round(Fixtures.roundId(1)),
            destinationOf(thread(UnreadFeedbackThreadResponse.TargetType.Reflection, targetId = UUID(3, 1))),
        )
        assertNull(destinationOf(thread(UnreadFeedbackThreadResponse.TargetType.Reflection, roundId = null)))
        assertEquals(
            UnreadDestination.Session(sessionId),
            destinationOf(thread(UnreadFeedbackThreadResponse.TargetType.PracticeSession, targetId = sessionId, roundId = null)),
        )
    }

    @Test
    fun `counts what's on threads it doesn't list`() {
        assertEquals(2, unreadNotListed(UnreadFeedbackResponse(5, listOf(thread(unread = 2), thread(unread = 1)))))
        assertEquals(0, unreadNotListed(UnreadFeedbackResponse(2, listOf(thread(unread = 2)))))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class UnreadOnHomeTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `Home loads what's new, and shows nothing when nothing is, or it can't be checked`() {
        val feedback = FakeFeedbackRepository().apply { unread = UnreadFeedbackResponse(2, listOf(thread())) }
        assertEquals(
            2,
            DashboardViewModel(FakeDashboardRepository(), feedback)
                .state.value.unreadFeedback
                ?.count,
        )

        assertNull(DashboardViewModel(FakeDashboardRepository(), FakeFeedbackRepository()).state.value.unreadFeedback)
        val failing = FakeFeedbackRepository().apply { unreadFailsWith = IOException() }
        assertNull(DashboardViewModel(FakeDashboardRepository(), failing).state.value.unreadFeedback)
    }

    @Test
    fun `coming back to Home picks up that it was read`() {
        val feedback = FakeFeedbackRepository().apply { unread = UnreadFeedbackResponse(2, listOf(thread())) }
        val viewModel = DashboardViewModel(FakeDashboardRepository(), feedback)

        feedback.unread = UnreadFeedbackResponse(0, emptyList())
        viewModel.refresh()

        assertNull(viewModel.state.value.unreadFeedback)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35], qualifiers = "en")
class NewFromYourCoachCardTest {
    @get:Rule
    val compose = createComposeRule()

    private var openedRound: UUID? = null
    private var openedSession: UUID? = null

    @Test
    fun `lists what's new, and opens the round or the session`() {
        val unread =
            UnreadFeedbackResponse(
                count = 4,
                threads =
                    listOf(
                        thread(),
                        thread(
                            UnreadFeedbackThreadResponse.TargetType.PracticeSession,
                            targetId = sessionId,
                            roundId = null,
                            coachName = null,
                            unread = 1,
                        ),
                    ),
            )
        compose.setContent {
            GolfProgressTheme {
                DashboardContent(
                    state = DashboardState(Load.Loaded(Fixtures.emptyDashboard()), unreadFeedback = unread),
                    onRetry = {},
                    onRefresh = {},
                    onLogRound = {},
                    onOpenRound = { openedRound = it },
                    onOpenPractice = { openedSession = it },
                )
            }
        }

        compose.onNodeWithText("New from your coach").assertIsDisplayed()
        compose.onNodeWithText("2 new messages").assertIsDisplayed()
        compose.onNodeWithText("And 1 more new message on other items.").assertIsDisplayed()

        compose.onNodeWithText("Pro on your round of", substring = true).performClick()
        assertEquals(Fixtures.roundId(1), openedRound)
        compose.onNodeWithText("A former coach on your practice session of", substring = true).performClick()
        assertEquals(sessionId, openedSession)
    }

    @Test
    fun `isn't there when nothing is new`() {
        compose.setContent {
            GolfProgressTheme {
                DashboardContent(
                    state = DashboardState(Load.Loaded(Fixtures.emptyDashboard())),
                    onRetry = {},
                    onRefresh = {},
                    onLogRound = {},
                    onOpenRound = {},
                )
            }
        }

        compose.onNodeWithText("New from your coach").assertDoesNotExist()
    }
}
