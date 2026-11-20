package com.golfprogress.app.feedback

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.hasAnyAncestor
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isDialog
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.data.FeedbackRepository
import com.golfprogress.app.testing.FakeCoachingRepository
import com.golfprogress.app.testing.FakeFeedbackRepository
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
import java.util.UUID

private val proId: UUID = UUID(1, 1)
private val kimId: UUID = UUID(1, 2)

/** The rules of feedback threads (#217): the web app's feedback.ts. */
class FeedbackRulesTest {
    @Test
    fun `a message is 1 to 2000 characters after trimming`() {
        assertEquals(FeedbackTextError.Empty, checkFeedbackText("   "))
        assertNull(checkFeedbackText(" " + "a".repeat(2000) + " "))
        assertEquals(FeedbackTextError.TooLong, checkFeedbackText("a".repeat(2001)))
    }

    @Test
    fun `each current coach who can see the item gets a thread, then past ones read-only`() {
        val threads =
            golferThreads(
                listOf(
                    Fixtures.feedback(1, "From Pro", proId),
                    Fixtures.feedback(2, "From the past", null, coachName = null),
                ),
                listOf(
                    Fixtures.coach("Pro", shareReflections = true, coachId = proId),
                    Fixtures.coach("Kim", shareReflections = true, coachId = kimId),
                    Fixtures.coach("Lou", shareReflections = false),
                ),
                FeedbackTarget.Reflection,
            )

        assertEquals(listOf(proId, kimId, null), threads.map { it.coachId })
        assertEquals(listOf(true, true, false), threads.map { it.canWrite })
        assertEquals(listOf(1, 0, 1), threads.map { it.messages.size })
    }

    @Test
    fun `nothing without coaches or messages`() {
        assertTrue(golferThreads(emptyList(), emptyList(), FeedbackTarget.Round).isEmpty())
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class FeedbackViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun started(
        feedback: FeedbackRepository,
        coaching: FakeCoachingRepository,
    ) = FeedbackViewModel(feedback, coaching).apply { start(Fixtures.userId, FeedbackTarget.Round, Fixtures.roundId(1)) }

    @Test
    fun `shows the coach's thread, marks it read, and replies`() {
        val feedback = FakeFeedbackRepository(mutableListOf(Fixtures.feedback(1, "Nice tempo.", proId, "Pro")))
        val viewModel = started(feedback, FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))))

        val thread = (viewModel.state.value.view as FeedbackView.Threads).threads.single()
        assertEquals("Nice tempo.", thread.messages.single().text)
        assertEquals(listOf(Fixtures.feedback(1, "", proId).createdAt), feedback.reads)

        viewModel.setDraft(proId, "  Thanks!  ")
        viewModel.send(proId)

        assertEquals(listOf(proId to "Thanks!"), feedback.written)
        assertNull(viewModel.state.value.drafts[proId])
        assertEquals(
            2,
            (viewModel.state.value.view as FeedbackView.Threads)
                .threads
                .single()
                .messages.size,
        )
    }

    @Test
    fun `is hidden without a coach or anything from one, even when loading fails`() {
        assertEquals(FeedbackView.Hidden, started(FakeFeedbackRepository(), FakeCoachingRepository()).state.value.view)
        val failing = FakeFeedbackRepository(threadFailsWith = IOException())
        assertEquals(FeedbackView.Hidden, started(failing, FakeCoachingRepository(listFailsWith = IOException())).state.value.view)
    }

    @Test
    fun `offers Try again when the thread can't load and there is a coach`() {
        val feedback = FakeFeedbackRepository(threadFailsWith = IOException())
        val viewModel = started(feedback, FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))))
        assertEquals(FeedbackView.Failed, viewModel.state.value.view)

        feedback.threadFailsWith = null
        viewModel.retry()

        assertTrue(viewModel.state.value.view is FeedbackView.Threads)
    }

    @Test
    fun `asks for text, and says when there are too many messages`() {
        val feedback = FakeFeedbackRepository()
        val viewModel = started(feedback, FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))))

        viewModel.send(proId)
        assertEquals(proId to FeedbackProblem.Empty, viewModel.state.value.sendProblem)

        feedback.failWith = ApiProblem(429)
        viewModel.setDraft(proId, "Hello")
        viewModel.send(proId)
        assertEquals(proId to FeedbackProblem.TooMany, viewModel.state.value.sendProblem)
        assertEquals("Hello", viewModel.state.value.drafts[proId])
    }

    @Test
    fun `edits and deletes your own message`() {
        val mine = Fixtures.feedback(1, "First", proId, fromCoach = false)
        val feedback = FakeFeedbackRepository(mutableListOf(mine))
        val viewModel = started(feedback, FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))))

        viewModel.startEdit(mine)
        viewModel.setEditText("Second")
        viewModel.saveEdit()
        assertEquals("Second", feedback.messages.single().text)
        assertNull(viewModel.state.value.editing)

        viewModel.askDelete(mine.feedbackMessageId)
        viewModel.confirmDelete()
        assertTrue(feedback.messages.isEmpty())
        assertNull(viewModel.state.value.confirmDelete)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35], qualifiers = "en")
class FeedbackSectionTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun show(
        feedback: FakeFeedbackRepository,
        coaching: FakeCoachingRepository,
        online: Boolean = true,
    ) {
        val viewModel = FeedbackViewModel(feedback, coaching)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides online) {
                GolfProgressTheme {
                    FeedbackSection(Fixtures.userId, FeedbackTarget.Round, Fixtures.roundId(1), viewModel)
                }
            }
        }
    }

    @Test
    fun `replies to the coach, then edits and deletes the reply`() {
        val feedback = FakeFeedbackRepository(mutableListOf(Fixtures.feedback(1, "Nice tempo.", proId, "Pro")))
        show(feedback, FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))))
        compose.onNodeWithText("Feedback from your coach").assertIsDisplayed()
        compose.onNodeWithText("Nice tempo.").assertIsDisplayed()

        compose.onNode(hasSetTextAction() and hasText("Message to Pro")).performTextInput("Thanks!")
        compose.onNodeWithText("Send").performClick()
        compose.onNodeWithText("Thanks!").assertIsDisplayed()
        assertEquals(listOf(proId to "Thanks!"), feedback.written)

        compose.onNode(hasContentDescription("Edit your message of", substring = true)).performClick()
        val editor = compose.onNode(hasSetTextAction() and hasText("Edit your message"))
        editor.performTextClearance()
        editor.performTextInput("Thank you!")
        compose.onNodeWithText("Save").performClick()
        compose.onNodeWithText("Thank you!").assertIsDisplayed()

        compose.onNode(hasContentDescription("Delete your message of", substring = true)).performClick()
        compose.onNode(hasText("Delete") and hasAnyAncestorDialog()).performClick()
        compose.onNodeWithText("Thank you!").assertDoesNotExist()
    }

    @Test
    fun `shows a thread per coach, and a former coach's without a reply field`() {
        show(
            FakeFeedbackRepository(
                mutableListOf(
                    Fixtures.feedback(1, "From Pro", proId, "Pro"),
                    Fixtures.feedback(2, "From the past", null, coachName = null),
                ),
            ),
            FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId), Fixtures.coach("Kim", coachId = kimId))),
        )

        compose.onNodeWithText("No messages yet. You can ask Kim about this.").assertExists()
        compose.onNodeWithText("From the past").assertExists()
        assertEquals(2, compose.onAllNodes(hasText("Send")).fetchSemanticsNodes().size)
    }

    @Test
    fun `isn't shown without a coach`() {
        show(FakeFeedbackRepository(), FakeCoachingRepository())
        compose.onNodeWithText("Feedback from your coach").assertDoesNotExist()
    }

    @Test
    fun `can't send while offline`() {
        show(FakeFeedbackRepository(), FakeCoachingRepository(listOf(Fixtures.coach("Pro", coachId = proId))), online = false)

        compose.onNodeWithText("Send").assertIsNotEnabled()
        compose.onNodeWithText("You're offline. Connect to send a message.").assertIsDisplayed()
    }

    private fun hasAnyAncestorDialog() = hasAnyAncestor(isDialog())
}
