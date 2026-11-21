package com.golfprogress.app.coaching

import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasAnyAncestor
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isDialog
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.feedback.CoachFeedbackSection
import com.golfprogress.app.feedback.FeedbackTarget
import com.golfprogress.app.feedback.FeedbackView
import com.golfprogress.app.feedback.FeedbackViewModel
import com.golfprogress.app.practice.PracticeListScreen
import com.golfprogress.app.practice.PracticeListViewModel
import com.golfprogress.app.rounds.RoundDetailContent
import com.golfprogress.app.rounds.RoundDetailViewModel
import com.golfprogress.app.rounds.RoundFormViewModel
import com.golfprogress.app.rounds.RoundsListScreen
import com.golfprogress.app.rounds.RoundsListViewModel
import com.golfprogress.app.testing.FakeCoachingRepository
import com.golfprogress.app.testing.FakeFeedbackRepository
import com.golfprogress.app.testing.FakeGoalsRepository
import com.golfprogress.app.testing.FakePracticeRepository
import com.golfprogress.app.testing.FakeRoundsRepository
import com.golfprogress.app.testing.FakeStudentRepository
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
import java.io.IOException

private val student = SavedStateHandle(mapOf(GOLFER_ID to Fixtures.studentId.toString()))

private fun studentAnd(vararg args: Pair<String, String>) = SavedStateHandle(mapOf(GOLFER_ID to Fixtures.studentId.toString()) + args)

/** The overview's trend summary (#208), as the web app's studentOverview.ts. */
class StudentRulesTest {
    @Test
    fun `the trend runs from the oldest to the newest scored round, and needs two`() {
        val rounds = Fixtures.timeline()
        val scored = rounds.mapNotNull { it.fitnessScoreTotal }
        assertEquals(FitnessTrend(scored.first(), scored.last(), scored.size), fitnessTrend(rounds))
        assertNull(fitnessTrend(rounds.take(1)))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class StudentViewModelsTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `students load a page at a time`() {
        val viewModel = StudentsViewModel(FakeStudentRepository(students = listOf(Fixtures.student("Ann"))))

        assertEquals(
            listOf("Ann"),
            viewModel.state.value.students
                .map { it.displayName },
        )
        assertTrue(viewModel.state.value.first is Load.Loaded)
    }

    @Test
    fun `the overview is gone once they're no longer a student`() {
        val viewModel = StudentOverviewViewModel(FakeStudentRepository(), student)

        assertEquals(Load.Loaded(null), viewModel.state.value.overview)
    }

    @Test
    fun `stopping coaching asks first, then leaves, and already removed counts as stopped`() {
        val students = FakeStudentRepository(overviews = mutableMapOf(Fixtures.studentId to Fixtures.studentOverview()))
        val viewModel = StudentOverviewViewModel(students, student)

        viewModel.askStop()
        assertTrue(viewModel.state.value.confirmStop)
        viewModel.confirmStop()
        assertEquals(listOf(Fixtures.studentId), students.stopped)
        assertTrue(viewModel.state.value.stopped)

        val removed = FakeStudentRepository(overviews = mutableMapOf(Fixtures.studentId to Fixtures.studentOverview()))
        removed.stopFailsWith = ApiProblem(404)
        val again = StudentOverviewViewModel(removed, student)
        again.confirmStop()
        assertTrue(again.state.value.stopped)
    }

    @Test
    fun `a failed stop keeps the dialog with the reason`() {
        val students = FakeStudentRepository(overviews = mutableMapOf(Fixtures.studentId to Fixtures.studentOverview()))
        students.stopFailsWith = IOException("offline")
        val viewModel = StudentOverviewViewModel(students, student)

        viewModel.askStop()
        viewModel.confirmStop()

        assertFalse(viewModel.state.value.stopped)
        assertTrue(viewModel.state.value.confirmStop)
        assertTrue(viewModel.state.value.stopError is IOException)
    }

    @Test
    fun `a student's screens read the student's rounds and sessions, not the coach's own`() {
        val rounds = FakeRoundsRepository(listOf(Fixtures.savedRound(1)))
        rounds.students[Fixtures.studentId] = FakeRoundsRepository(listOf(Fixtures.savedRound(5), Fixtures.savedRound(6)))
        val practice = FakePracticeRepository(listOf(Fixtures.practiceSession(1)))
        practice.students[Fixtures.studentId] = FakePracticeRepository(emptyList())
        val goals = FakeGoalsRepository(goals = listOf(Fixtures.goal(1, title = "The coach's own goal")))
        goals.student(Fixtures.studentId)
        goals.students.getValue(Fixtures.studentId).goals = listOf(Fixtures.goal(2, title = "For Ann"))

        val list = RoundsListViewModel(rounds, student)
        assertEquals(
            listOf(Fixtures.roundId(5), Fixtures.roundId(6)),
            list.state.value.rounds
                .map { it.roundId },
        )

        val detail = RoundDetailViewModel(rounds, studentAnd(RoundFormViewModel.ROUND_ID to Fixtures.roundId(5).toString()))
        assertEquals(Fixtures.roundId(5), (detail.state.value.detail as Load.Loaded).value?.round?.roundId)

        val practiceList = PracticeListViewModel(practice, goals, student)
        assertTrue(
            practiceList.state.value.sessions
                .isEmpty(),
        )
        assertEquals(
            listOf("For Ann"),
            practiceList.state.value.goals
                .map { it.title },
        )
    }

    @Test
    fun `a coach's feedback is their one thread with the student, and they can write first`() {
        val feedback = FakeFeedbackRepository()
        val coaching = FakeCoachingRepository()
        val viewModel = FeedbackViewModel(feedback, coaching)

        viewModel.startAsCoach(Fixtures.studentId, "Ann", Fixtures.userId, FeedbackTarget.Round, Fixtures.roundId(1))

        val view = viewModel.state.value.view as FeedbackView.Threads
        assertEquals("Ann", view.studentName)
        val thread = view.threads.single()
        assertTrue(thread.canWrite)
        assertTrue(thread.messages.isEmpty())

        viewModel.setDraft(Fixtures.userId, "Nice tempo")
        viewModel.send(Fixtures.userId)
        assertEquals(listOf(Fixtures.userId to "Nice tempo"), feedback.written)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class StudentScreensTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun themed(content: @androidx.compose.runtime.Composable () -> Unit) =
        compose.setContent { CompositionLocalProvider(LocalOnline provides true) { GolfProgressTheme { content() } } }

    @Test
    fun `students show their last activity, unread replies and what they share`() {
        var opened: java.util.UUID? = null
        val viewModel =
            StudentsViewModel(FakeStudentRepository(students = listOf(Fixtures.student("Ann", unreadReplies = 2, sharePractice = false))))
        themed { StudentsScreen(onOpenStudent = { opened = it }, viewModel = viewModel) }

        compose.onNodeWithText("Ann").assertIsDisplayed()
        compose.onNodeWithText("2 unread replies").assertIsDisplayed()
        compose.onNodeWithText("You see: Rounds and progress, Reflections and notes").assertIsDisplayed()

        compose.onNodeWithText("Ann").performClick()
        assertEquals(Fixtures.studentId, opened)
    }

    @Test
    fun `a paused student says so, and doesn't open`() {
        var opened: java.util.UUID? = null
        val viewModel = StudentsViewModel(FakeStudentRepository(students = listOf(Fixtures.student("Ann", paused = true))))
        themed { StudentsScreen(onOpenStudent = { opened = it }, viewModel = viewModel) }

        compose.onNodeWithText("Paused", substring = true).assertIsDisplayed()
        compose.onNodeWithText("Ann").performClick()
        assertNull(opened)
    }

    @Test
    fun `without students, it says how they get here`() {
        val viewModel = StudentsViewModel(FakeStudentRepository())
        themed { StudentsScreen(onOpenStudent = {}, viewModel = viewModel) }

        compose.onNodeWithText("No students yet", substring = true).assertIsDisplayed()
    }

    @Test
    fun `the overview shows their next focus and goals, and stops coaching after asking`() {
        val students =
            FakeStudentRepository(
                overviews = mutableMapOf(Fixtures.studentId to Fixtures.studentOverview(goals = listOf(Fixtures.goal(1)))),
            )
        var stopped = false
        val viewModel = StudentOverviewViewModel(students, student)
        themed { StudentOverviewScreen(StudentOverviewActions(onStopped = { stopped = true }), viewModel = viewModel) }

        compose.onNodeWithText("Ann").assertIsDisplayed()
        compose.onNodeWithText("Their next focus").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Lag putts inside 1 metre").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Set a practice goal").performScrollTo().assertIsDisplayed()

        compose.onNodeWithText("Stop coaching").performScrollTo().performClick()
        compose.onNodeWithText("You lose access", substring = true).assertExists()
        compose.onNode(hasText("Stop coaching") and hasAnyAncestor(isDialog())).performClick()

        compose.waitForIdle()
        assertTrue(stopped)
        assertEquals(listOf(Fixtures.studentId), students.stopped)
    }

    @Test
    fun `the overview says what isn't shared`() {
        val students =
            FakeStudentRepository(
                overviews =
                    mutableMapOf(
                        Fixtures.studentId to Fixtures.studentOverview(withRounds = false, shareReflections = false, sharePractice = false),
                    ),
            )
        val viewModel = StudentOverviewViewModel(students, student)
        themed { StudentOverviewScreen(StudentOverviewActions(), viewModel = viewModel) }

        compose.onNodeWithText("Ann hasn't logged a round yet", substring = true).assertIsDisplayed()
        compose.onNodeWithText("Ann doesn't share reflections with you.").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Set a practice goal").assertDoesNotExist()
    }

    @Test
    fun `a student's rounds are read-only`() {
        val rounds = FakeRoundsRepository()
        rounds.students[Fixtures.studentId] = FakeRoundsRepository()
        val viewModel = RoundsListViewModel(rounds, student)
        themed { RoundsListScreen(onOpenRound = {}, onLogRound = {}, viewModel = viewModel) }

        compose.onNodeWithText("No rounds logged yet.").assertIsDisplayed()
        compose.onNodeWithText("Log your first round").assertDoesNotExist()
    }

    @Test
    fun `a student's round has no Edit, Delete or Add reflection`() {
        val rounds = FakeRoundsRepository()
        rounds.students[Fixtures.studentId] = FakeRoundsRepository(listOf(Fixtures.savedRound(1)))
        val viewModel = RoundDetailViewModel(rounds, studentAnd(RoundFormViewModel.ROUND_ID to Fixtures.roundId(1).toString()))
        val state = viewModel.state.value
        themed { RoundDetailContent(state, {}, {}, {}, {}, {}, readOnly = true) }

        compose.onNodeWithText("Edit round").assertDoesNotExist()
        compose.onNodeWithText("Delete round").assertDoesNotExist()
        compose.onNodeWithText("Add a reflection", substring = true).assertDoesNotExist()
    }

    @Test
    fun `practice that's no longer shared says so`() {
        val practice = FakePracticeRepository()
        practice.students[Fixtures.studentId] = FakePracticeRepository().apply { failWith = ApiProblem(404) }
        val viewModel = PracticeListViewModel(practice, FakeGoalsRepository(), student)
        themed { PracticeListScreen(onOpen = {}, onLogPractice = {}, viewModel = viewModel) }

        compose.onNodeWithText("This golfer doesn't share practice with you.").assertIsDisplayed()
        compose.onNodeWithText("Log practice").assertDoesNotExist()
    }

    @Test
    fun `a student's practice offers Set a practice goal instead of Log practice`() {
        val practice = FakePracticeRepository()
        practice.students[Fixtures.studentId] = FakePracticeRepository(listOf(Fixtures.practiceSession(1)))
        var setGoal = false
        val viewModel = PracticeListViewModel(practice, FakeGoalsRepository(), student)
        themed {
            PracticeListScreen(
                onOpen = {},
                onLogPractice = {},
                viewModel = viewModel,
                onSetGoal = { setGoal = true },
            )
        }

        compose.onNodeWithText("No goals yet", substring = true).assertIsDisplayed()
        compose.onNodeWithText("Log practice").assertDoesNotExist()
        compose.onNodeWithText("Set a practice goal").performClick()
        assertTrue(setGoal)
    }

    @Test
    fun `a coach's thread shows the student's replies under their name`() {
        val feedback =
            FakeFeedbackRepository(
                messages =
                    mutableListOf(
                        Fixtures.feedback(1, "Tempo looked good", Fixtures.userId, coachName = "Pro", fromCoach = true, mine = true),
                        Fixtures.feedback(2, "Thanks!", Fixtures.userId, coachName = "Pro", fromCoach = false, mine = false),
                    ),
            )
        val viewModel = FeedbackViewModel(feedback, FakeCoachingRepository())
        themed {
            // Under the round, as on the screen, which scrolls.
            androidx.compose.foundation.layout.Column(
                androidx.compose.ui.Modifier
                    .verticalScroll(androidx.compose.foundation.rememberScrollState()),
            ) { CoachFeedbackSection(Fixtures.studentId, "Ann", Fixtures.userId, FeedbackTarget.Round, Fixtures.roundId(1), viewModel) }
        }

        compose.onNodeWithText("Feedback").assertIsDisplayed()
        compose.onNodeWithText("You", substring = true).assertExists()
        compose.onNodeWithText("Ann · ", substring = true).assertExists()
        compose.onNode(hasSetTextAction()).performTextInput("Keep it up")
        compose.onNodeWithText("Send").performScrollTo().performClick()
        assertEquals("Keep it up", feedback.written.single().second)
    }
}
