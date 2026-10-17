package com.golfprogress.app.practice

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performImeAction
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
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

/** The Log practice rules (#105): the web app's and the API's. */
class PracticeRulesTest {
    private val values = PracticeFormValues(practicedAt = LocalDate.of(2026, 5, 3))

    @Test
    fun `duration is optional, whole, and 1 to 1440 minutes`() {
        assertEquals(emptyMap<PracticeField, PracticeError>(), validatePractice(values))
        assertEquals(PracticeError.WholeNumber, validatePractice(values.copy(durationMinutes = "45.5"))[PracticeField.DurationMinutes])
        assertEquals(PracticeError.DurationRange(1440), validatePractice(values.copy(durationMinutes = "0"))[PracticeField.DurationMinutes])
        assertEquals(
            PracticeError.DurationRange(1440),
            validatePractice(values.copy(durationMinutes = "1441"))[PracticeField.DurationMinutes],
        )
    }

    @Test
    fun `text is bounded as the API bounds it`() {
        assertEquals(PracticeError.TooLong(200), validatePractice(values.copy(venueName = "x".repeat(201)))[PracticeField.VenueName])
        assertEquals(PracticeError.TooLong(2000), validatePractice(values.copy(notes = "x".repeat(2001)))[PracticeField.Notes])
    }

    @Test
    fun `tags are trimmed, unique in any case, at most 50 characters and 20 tags`() {
        assertEquals(AddTag.Added(listOf("Putting")), addTag(emptyList(), "  Putting "))
        assertEquals(AddTag.Refused.Duplicate, addTag(listOf("Putting"), "putting"))
        assertEquals(AddTag.Refused.TooLong, addTag(emptyList(), "x".repeat(51)))
        assertEquals(AddTag.Refused.TooMany, addTag((1..20).map { "t$it" }, "one more"))
        assertEquals(AddTag.Refused.Empty, addTag(emptyList(), "  "))
    }

    @Test
    fun `the request leaves empty text out`() {
        val request = values.copy(durationMinutes = "45", venueName = " ", tags = listOf("lag")).toRequest()

        assertEquals("2026-05-03", request.practicedAt)
        assertEquals(45, request.durationMinutes)
        assertNull(request.venueName)
        assertEquals(listOf("lag"), request.tags)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class PracticeViewModelsTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `the list pages until the total`() {
        val repository = FakePracticeRepository((1..25).map { Fixtures.practiceSession(it) })
        val viewModel = PracticeListViewModel(repository)
        assertEquals(20, viewModel.state.value.sessions.size)

        viewModel.loadMore()

        assertEquals(25, viewModel.state.value.sessions.size)
        assertEquals(false, viewModel.state.value.hasMore)
    }

    @Test
    fun `a comma adds a tag, and a tag still being typed is kept on save`() {
        val repository = FakePracticeRepository()
        val viewModel = LogPracticeViewModel(repository)

        viewModel.setTagInput("lag,")
        viewModel.setTagInput("speed")
        viewModel.save()

        assertEquals(listOf("lag", "speed"), repository.created.single().tags)
        assertEquals(Fixtures.practiceSession(8).practiceSessionId, viewModel.state.value.savedId)
    }

    @Test
    fun `a duplicate tag is refused and stops the save`() {
        val repository = FakePracticeRepository()
        val viewModel = LogPracticeViewModel(repository)

        viewModel.setTagInput("lag,")
        viewModel.setTagInput("LAG")
        viewModel.save()

        assertEquals(TagProblem.Duplicate, viewModel.state.value.tagProblem)
        assertEquals(PracticeField.Tags, viewModel.state.value.focus)
        assertTrue(repository.created.isEmpty())
    }

    @Test
    fun `the detail deletes after asking, and says when the session is gone`() {
        val session = Fixtures.practiceSession(1)
        val repository = FakePracticeRepository(listOf(session))
        val args = SavedStateHandle(mapOf(PracticeDetailViewModel.SESSION_ID to session.practiceSessionId.toString()))
        val viewModel = PracticeDetailViewModel(repository, args)
        assertEquals(session, (viewModel.state.value.session as Load.Loaded).value)

        viewModel.askToDelete()
        viewModel.confirmDelete()
        assertTrue(viewModel.state.value.deleted)
        assertEquals(listOf(session.practiceSessionId), repository.deleted)

        val gone = PracticeDetailViewModel(repository, args)
        assertEquals(Load.Loaded(null), gone.state.value.session)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class PracticeScreensTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun theListShowsTypeAndDuration_AndAnUnknownTypeAsPractice() {
        val repository =
            FakePracticeRepository(
                listOf(
                    Fixtures.practiceSession(1),
                    Fixtures.practiceSession(2, sessionType = "Fitness"),
                ),
            )
        val viewModel = PracticeListViewModel(repository)
        compose.setContent { GolfProgressTheme { PracticeListScreen(onOpen = {}, onLogPractice = {}, viewModel = viewModel) } }

        compose.onNode(hasContentDescription("May 19, 2026, Putting, 45 min")).assertIsDisplayed()
        compose.onNode(hasContentDescription("May 18, 2026, Practice, 45 min")).assertIsDisplayed()
        compose.onNodeWithText("Practice counts towards your Habits score.").assertIsDisplayed()
    }

    @Test
    fun logsASessionWithTags() {
        val repository = FakePracticeRepository()
        val viewModel = LogPracticeViewModel(repository)
        var saved: UUID? = null
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { LogPracticeScreen(onSaved = { saved = it }, viewModel = viewModel) }
            }
        }

        compose.onNodeWithText("Short game").performClick()
        compose.onNode(hasText("Duration") and hasSetTextAction()).performTextInput("60")
        val tags = compose.onNode(hasText("Tags") and hasSetTextAction())
        tags.performScrollTo().performTextInput("bunkers")
        tags.performImeAction()
        compose.onNodeWithContentDescription("Remove tag bunkers").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Save").performScrollTo().performClick()

        compose.waitForIdle()
        val request = repository.created.single()
        assertEquals(CreatePracticeSessionRequest.SessionType.ShortGame, request.sessionType)
        assertEquals(60, request.durationMinutes)
        assertEquals(listOf("bunkers"), request.tags)
        assertEquals(Fixtures.practiceSession(8).practiceSessionId, saved)
    }
}
