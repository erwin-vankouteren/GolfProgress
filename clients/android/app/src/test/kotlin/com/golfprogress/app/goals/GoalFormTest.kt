package com.golfprogress.app.goals

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performScrollTo
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.PracticeGoalDetailResponse
import com.golfprogress.api.generated.models.PracticeGoalRequest
import com.golfprogress.app.coaching.GOLFER_ID
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.testing.FakeGoalsRepository
import com.golfprogress.app.testing.Fixtures
import com.golfprogress.app.ui.common.LocalOnline
import com.golfprogress.app.ui.theme.GolfProgressTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException
import java.time.LocalDate

private val today: LocalDate = LocalDate.parse("2026-05-10")

/** Setting a practice goal (#208): the web app's goals.ts rules. */
class GoalFormRulesTest {
    @Test
    fun `a new goal runs four weeks from today, six sessions`() {
        val goal = emptyGoal(today)
        assertEquals(today.plusDays(28), goal.endsOn)
        assertEquals("6", goal.targetSessions)
        assertTrue(validateGoal(goal.copy(title = "Putting"), today).isEmpty())
    }

    @Test
    fun `title, target and period are checked`() {
        val goal = emptyGoal(today)
        assertEquals(GoalError.Required, validateGoal(goal.copy(title = "  "), today)[GoalField.Title])
        assertEquals(GoalError.TooLong(100), validateGoal(goal.copy(title = "x".repeat(101)), today)[GoalField.Title])
        assertEquals(GoalError.TargetRange(50), validateGoal(goal.copy(targetSessions = "0"), today)[GoalField.TargetSessions])
        assertEquals(GoalError.TargetRange(50), validateGoal(goal.copy(targetSessions = "six"), today)[GoalField.TargetSessions])
        assertEquals(GoalError.EndBeforeStart, validateGoal(goal.copy(endsOn = today.minusDays(1)), today)[GoalField.EndsOn])
        assertEquals(GoalError.PeriodTooLong(8), validateGoal(goal.copy(endsOn = today.plusDays(57)), today)[GoalField.EndsOn])
        assertEquals(
            GoalError.EndInPast,
            validateGoal(goal.copy(startsOn = today.minusDays(10), endsOn = today.minusDays(1)), today)[GoalField.EndsOn],
        )
    }

    @Test
    fun `optional choices left empty go as null, and text is trimmed`() {
        val request = emptyGoal(today).copy(title = " Putting ", details = "  ").toRequest()
        assertEquals("Putting", request.title)
        assertNull(request.details)
        assertNull(request.focusArea)
        assertNull(request.sessionType)
        assertEquals(6, request.targetSessions)
    }

    @Test
    fun `an existing goal fills the form`() {
        val values = goalToForm(Fixtures.goal(1, title = "Lag putts"))
        assertEquals("Lag putts", values.title)
        assertEquals(PracticeGoalRequest.FocusArea.Putting, values.focusArea)
        assertEquals(PracticeGoalRequest.SessionType.Putting, values.sessionType)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class GoalFormViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private val student = SavedStateHandle(mapOf(GOLFER_ID to Fixtures.studentId.toString()))

    @Test
    fun `setting a goal sends it for the student, and nothing until it's valid`() {
        val goals = FakeGoalsRepository()
        val viewModel = GoalFormViewModel(goals, student)

        viewModel.save()
        assertTrue(
            goals.student(Fixtures.studentId).let {
                goals.students
                    .getValue(Fixtures.studentId)
                    .set
                    .isEmpty()
            },
        )
        assertTrue(viewModel.state.value.submitted)

        viewModel.setTitle("Bunker play")
        viewModel.setFocusArea(PracticeGoalRequest.FocusArea.Penalties)
        viewModel.save()

        val sent =
            goals.students
                .getValue(Fixtures.studentId)
                .set
                .single()
        assertEquals("Bunker play", sent.title)
        assertEquals(PracticeGoalRequest.FocusArea.Penalties, sent.focusArea)
        assertEquals(Fixtures.goalId(9), viewModel.state.value.savedId)
    }

    @Test
    fun `changing a goal starts from it and sends the whole goal`() {
        val goals = FakeGoalsRepository()
        val goal = Fixtures.goal(1, title = "Lag putts")
        goals.student(Fixtures.studentId)
        goals.students.getValue(Fixtures.studentId).details[goal.practiceGoalId] = PracticeGoalDetailResponse(goal, emptyList())
        val viewModel =
            GoalFormViewModel(
                goals,
                SavedStateHandle(
                    mapOf(
                        GOLFER_ID to Fixtures.studentId.toString(),
                        "goalId" to goal.practiceGoalId.toString(),
                    ),
                ),
            )

        assertEquals("Lag putts", viewModel.state.value.values.title)
        viewModel.setTargetSessions("8")
        viewModel.save()

        val (id, request) =
            goals.students
                .getValue(Fixtures.studentId)
                .changed
                .single()
        assertEquals(goal.practiceGoalId, id)
        assertEquals(8, request.targetSessions)
        assertEquals("Lag putts", request.title)
    }

    @Test
    fun `the API's refusal shows on its field, and anything else under the form`() {
        val goals = FakeGoalsRepository()
        val viewModel = GoalFormViewModel(goals, student)
        viewModel.setTitle("Putting")

        goals.student(Fixtures.studentId)
        val studentGoals = goals.students.getValue(Fixtures.studentId)
        studentGoals.failWith = ApiProblem(400, errors = mapOf("endsOn" to listOf("Too far ahead.")))
        viewModel.save()
        assertEquals("Too far ahead.", viewModel.state.value.apiErrors[GoalField.EndsOn])

        studentGoals.failWith = IOException("offline")
        viewModel.save()
        assertEquals(FormProblem.CantConnect, viewModel.state.value.problem)
    }

    @Test
    fun `the coach removes their goal after asking, but not once sessions count towards it`() {
        val goals = FakeGoalsRepository()
        val goal = Fixtures.goal(1)
        goals.student(Fixtures.studentId)
        val studentGoals = goals.students.getValue(Fixtures.studentId)
        studentGoals.details[goal.practiceGoalId] = PracticeGoalDetailResponse(goal, emptyList())
        val args =
            SavedStateHandle(
                mapOf(
                    GOLFER_ID to Fixtures.studentId.toString(),
                    GoalDetailViewModel.GOAL_ID to goal.practiceGoalId.toString(),
                ),
            )

        val blocked = GoalDetailViewModel(goals, args)
        studentGoals.failWith = ApiProblem(409)
        blocked.askRemove()
        blocked.confirmRemove()
        assertEquals(RemoveGoalProblem.HasSessions, blocked.state.value.removeProblem)
        assertTrue(blocked.state.value.confirmRemove)

        studentGoals.failWith = null
        blocked.confirmRemove()
        assertEquals(listOf(goal.practiceGoalId), studentGoals.removed)
        assertTrue(blocked.state.value.removed)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class CoachGoalScreensTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `the coach's goal offers Change and Remove, not Mark done`() {
        val goals = FakeGoalsRepository()
        val goal = Fixtures.goal(1)
        goals.student(Fixtures.studentId)
        goals.students.getValue(Fixtures.studentId).details[goal.practiceGoalId] = PracticeGoalDetailResponse(goal, emptyList())
        val viewModel =
            GoalDetailViewModel(
                goals,
                SavedStateHandle(
                    mapOf(
                        GOLFER_ID to Fixtures.studentId.toString(),
                        GoalDetailViewModel.GOAL_ID to goal.practiceGoalId.toString(),
                    ),
                ),
            )
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { GoalDetailScreen(onOpenSession = {}, viewModel = viewModel) }
            }
        }

        compose.onNodeWithText("No sessions towards it yet.").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Change goal").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Remove goal").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Mark done").assertDoesNotExist()
        compose.onNodeWithText("From Pro", substring = true).assertDoesNotExist()
    }

    @Test
    fun `the form says what's wrong once Save is pressed`() {
        val viewModel = GoalFormViewModel(FakeGoalsRepository(), SavedStateHandle(mapOf(GOLFER_ID to Fixtures.studentId.toString())))
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { GoalFormScreen(onSaved = {}, viewModel = viewModel) }
            }
        }

        compose.onNodeWithText("Give the goal a title.").assertDoesNotExist()
        viewModel.save()
        compose.onNodeWithText("Give the goal a title.").assertIsDisplayed()
    }
}
