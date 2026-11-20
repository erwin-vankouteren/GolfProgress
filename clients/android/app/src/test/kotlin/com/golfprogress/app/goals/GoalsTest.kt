package com.golfprogress.app.goals

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasAnyAncestor
import androidx.compose.ui.test.hasScrollAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isDialog
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToNode
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.GoalOutcomeResponse
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalResponse
import com.golfprogress.app.practice.LogPracticeViewModel
import com.golfprogress.app.practice.PracticeListScreen
import com.golfprogress.app.practice.PracticeListViewModel
import com.golfprogress.app.testing.FakeGoalsRepository
import com.golfprogress.app.testing.FakePracticeRepository
import com.golfprogress.app.testing.Fixtures
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LocalOnline
import com.golfprogress.app.ui.theme.GolfProgressTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.time.LocalDate
import java.util.UUID

private val goalId: UUID = UUID(4, 1)
private val today: LocalDate = LocalDate.now()

private fun goal(
    id: UUID = goalId,
    status: PracticeGoalResponse.Status = PracticeGoalResponse.Status.Active,
    sessionType: String? = "Putting",
    linked: Int = 2,
    startsOn: LocalDate = today.minusDays(7),
    endsOn: LocalDate = today.plusDays(21),
) = PracticeGoalResponse(
    practiceGoalId = id,
    golferId = Fixtures.userId,
    coachId = UUID(1, 1),
    coachName = "Pro",
    title = "Lag putts inside 1 metre",
    details = "From 10 metres, 20 balls.",
    focusArea = PracticeGoalResponse.FocusArea.Putting,
    sessionType = sessionType,
    targetSessions = 6,
    linkedSessions = linked,
    startsOn = startsOn.toString(),
    endsOn = endsOn.toString(),
    status = status,
    createdAt = Fixtures.now,
    updatedAt = null,
)

private fun outcome(verdict: GoalOutcomeResponse.Verdict = GoalOutcomeResponse.Verdict.Improved) =
    GoalOutcomeResponse(
        focusArea = GoalOutcomeResponse.FocusArea.Putting,
        stat = GoalOutcomeResponse.Stat.PuttsPer18,
        higherIsBetter = false,
        before = 35.0,
        during = 32.0,
        change = -3.0,
        roundsBefore = 5,
        roundsDuring = 4,
        duringFrom = today.minusDays(7).toString(),
        duringTo = today.plusDays(35).toString(),
        confidence = GoalOutcomeResponse.Confidence.High,
        verdict = verdict,
    )

private fun repository(goal: PracticeGoalResponse = goal()) =
    FakeGoalsRepository(
        goals = listOf(goal),
        details = mutableMapOf(goalId to PracticeGoalDetailResponse(goal, listOf(Fixtures.practiceSession(1)))),
        outcomes = mutableMapOf(goalId to outcome()),
    )

/** Practice goals' rules (#231): the web app's goals.ts. */
class GoalRulesTest {
    @Test
    fun `active and ended goals show on Practice, not done or dismissed ones`() {
        val goals = PracticeGoalResponse.Status.entries.mapIndexed { i, status -> goal(id = UUID(9, i.toLong()), status = status) }
        assertEquals(
            listOf(PracticeGoalResponse.Status.Active, PracticeGoalResponse.Status.Ended),
            goalsToShow(goals).map { it.status },
        )
    }

    @Test
    fun `a session counts towards an active goal whose period includes its date`() {
        val goals = listOf(goal(), goal(id = UUID(9, 9), status = PracticeGoalResponse.Status.Ended))
        assertEquals(listOf(goalId), linkableGoals(goals, today).map { it.practiceGoalId })
        assertTrue(linkableGoals(goals, today.minusDays(8)).isEmpty())
        assertEquals(1, linkableGoals(goals, today.plusDays(21)).size)
    }

    @Test
    fun `the goal for the session's type is suggested, until the golfer chooses`() {
        val goals = listOf(goal(id = UUID(9, 9), sessionType = null), goal())
        assertEquals(goalId, selectedGoal(goals, today, "Putting", chosen = null))
        assertNull(selectedGoal(goals, today, "Range", chosen = null))
        assertNull(selectedGoal(goals, today, "Putting", chosen = NO_GOAL))
        assertEquals(UUID(9, 9), selectedGoal(goals, today, "Putting", chosen = UUID(9, 9)))
        // A choice that no longer applies, e.g. after changing the date, counts as none.
        assertNull(selectedGoal(goals, today.plusDays(30), "Putting", chosen = goalId))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class GoalViewModelsTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `the goal page loads the goal and whether it worked, and marks it done`() {
        val goals = repository()
        val viewModel = GoalDetailViewModel(goals, SavedStateHandle(mapOf(GoalDetailViewModel.GOAL_ID to goalId.toString())))

        assertEquals(PracticeGoalResponse.Status.Active, (viewModel.state.value.goal as Load.Loaded).value?.goal?.status)
        assertTrue(viewModel.state.value.outcome is Load.Loaded)

        viewModel.markDone()

        assertEquals(listOf(goalId to true), goals.statusChanges)
        assertEquals(PracticeGoalResponse.Status.Done, (viewModel.state.value.goal as Load.Loaded).value?.goal?.status)
    }

    @Test
    fun `dismissing asks first`() {
        val goals = repository()
        val viewModel = GoalDetailViewModel(goals, SavedStateHandle(mapOf(GoalDetailViewModel.GOAL_ID to goalId.toString())))

        viewModel.askDismiss()
        assertTrue(viewModel.state.value.confirmDismiss)
        viewModel.confirmDismiss()

        assertEquals(listOf(goalId to false), goals.statusChanges)
        assertFalse(viewModel.state.value.confirmDismiss)
    }

    @Test
    fun `logging a putting session suggests the goal, and sends it`() {
        val practice = FakePracticeRepository()
        val viewModel = LogPracticeViewModel(practice, repository())

        assertNull(viewModel.state.value.goalId)
        viewModel.setType(CreatePracticeSessionRequest.SessionType.Putting)
        assertEquals(goalId, viewModel.state.value.goalId)
        assertTrue(viewModel.state.value.goalSuggested)

        viewModel.save()

        assertEquals(goalId, practice.created.single().practiceGoalId)
    }

    @Test
    fun `choosing no goal sends none`() {
        val practice = FakePracticeRepository()
        val viewModel = LogPracticeViewModel(practice, repository())
        viewModel.setType(CreatePracticeSessionRequest.SessionType.Putting)

        viewModel.chooseGoal(NO_GOAL)
        viewModel.save()

        assertNull(practice.created.single().practiceGoalId)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35], qualifiers = "en")
class GoalScreensTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `Practice shows the goal from the coach, with its progress, and opens it`() {
        var opened: UUID? = null
        val viewModel = PracticeListViewModel(FakePracticeRepository(listOf(Fixtures.practiceSession(1))), repository())
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme {
                    PracticeListScreen(
                        onOpen = {},
                        onLogPractice = {},
                        viewModel = viewModel,
                        onOpenGoal = { opened = it },
                    )
                }
            }
        }

        compose.onNodeWithText("Goals from your coach").assertIsDisplayed()
        compose.onNodeWithText("2 of 6 sessions").assertIsDisplayed()
        compose.onNodeWithText("Lag putts inside 1 metre").performClick()
        assertEquals(goalId, opened)
    }

    @Test
    fun `the goal page says whether it worked, and dismisses after asking`() {
        val goals = repository()
        val viewModel = GoalDetailViewModel(goals, SavedStateHandle(mapOf(GoalDetailViewModel.GOAL_ID to goalId.toString())))
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { GoalDetailScreen(onOpenSession = {}, viewModel = viewModel) }
            }
        }

        compose.onNodeWithText("Lag putts inside 1 metre").assertIsDisplayed()
        compose.onNodeWithText("From Pro", substring = true).assertIsDisplayed()
        compose.onNode(hasScrollAction()).performScrollToNode(hasText("It's working", substring = true))
        compose.onNodeWithText("It's working: Putts per 18 holes improved on before the goal.").assertIsDisplayed()

        compose.onNode(hasScrollAction()).performScrollToNode(hasText("Dismiss"))
        compose.onNodeWithText("Dismiss").performClick()
        compose.onNode(hasText("Dismiss") and hasAnyAncestor(isDialog())).performClick()

        assertEquals(listOf(goalId to false), goals.statusChanges)
        compose.onNodeWithText("Mark done").assertDoesNotExist()
    }

    @Test
    fun `too few rounds can't tell yet`() {
        val goals = repository().apply { outcomes[goalId] = outcome(GoalOutcomeResponse.Verdict.CantTellYet) }
        val viewModel = GoalDetailViewModel(goals, SavedStateHandle(mapOf(GoalDetailViewModel.GOAL_ID to goalId.toString())))
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { GoalDetailScreen(onOpenSession = {}, viewModel = viewModel) }
            }
        }

        compose.onNode(hasScrollAction()).performScrollToNode(hasText("Can't tell yet", substring = true))
        compose.onNodeWithText("Can't tell yet", substring = true).assertIsDisplayed()
    }
}
