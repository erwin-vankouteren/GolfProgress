package com.golfprogress.app.rounds

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.testing.FakeRoundsRepository
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
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

private val roundArgs = SavedStateHandle(mapOf(RoundFormViewModel.ROUND_ID to Fixtures.roundId(1).toString()))

/** The reflection rules (#101): the web app's and the API's. */
class ReflectionRulesTest {
    @Test
    fun `needs at least one thing, and spaces don't count`() {
        assertFalse(ReflectionValues().hasContent)
        assertFalse(ReflectionValues(text = mapOf(ReflectionText.WhatWorked to "   ")).hasContent)
        assertTrue(ReflectionValues(frustrationSpillover = false).hasContent)
        assertTrue(ReflectionValues(moodRating = 3).hasContent)
    }

    @Test
    fun `text is at most 1000 characters, counted as people count them`() {
        assertEquals(
            listOf(ReflectionText.WhatToRepeat),
            ReflectionValues(text = mapOf(ReflectionText.WhatToRepeat to "x".repeat(1001))).tooLong,
        )
        // 1,000 golfers are 2,000 UTF-16 units, but 1,000 characters.
        assertEquals(emptyList<ReflectionText>(), ReflectionValues(text = mapOf(ReflectionText.WhatWorked to "🏌".repeat(1000))).tooLong)
    }

    @Test
    fun `sends trimmed text and nothing for what's empty`() {
        val request =
            ReflectionValues(
                focusRating = 4,
                text =
                    mapOf(
                        ReflectionText.WhatWorked to " Tempo ",
                        ReflectionText.OneChangeNext to " ",
                    ),
            ).toRequest()

        assertEquals(4, request.focusRating)
        assertEquals("Tempo", request.whatWorked)
        assertNull(request.oneChangeNext)
        assertNull(request.frustrationSpillover)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class ReflectionViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `an empty reflection isn't sent`() {
        val rounds = FakeRoundsRepository()
        val viewModel = ReflectionViewModel(rounds, roundArgs)

        viewModel.save()

        assertTrue(viewModel.state.value.needsSomething)
        assertTrue(rounds.added.isEmpty())
    }

    @Test
    fun `saves to the round once`() {
        val rounds = FakeRoundsRepository()
        val viewModel = ReflectionViewModel(rounds, roundArgs)

        viewModel.setFocus(4)
        viewModel.setFrustration(false)
        viewModel.save()
        viewModel.save()

        val (round, request) = rounds.added.single()
        assertEquals(Fixtures.roundId(1), round)
        assertEquals(4, request.focusRating)
        assertEquals(false, request.frustrationSpillover)
        assertTrue(viewModel.state.value.saved)
    }

    @Test
    fun `shows the API's messages next to their fields`() {
        val rounds = FakeRoundsRepository().apply { failWith = ApiProblem(400, errors = mapOf("WhatWorked" to listOf("Too long."))) }
        val viewModel = ReflectionViewModel(rounds, roundArgs)

        viewModel.setText(ReflectionText.WhatWorked, "Tempo")
        viewModel.save()

        assertEquals("Too long.", viewModel.state.value.apiErrors[ReflectionText.WhatWorked])
        assertFalse(viewModel.state.value.saved)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ReflectionScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun ratesAndWrites_ThenSaves() {
        val rounds = FakeRoundsRepository()
        var saved = false
        val viewModel = ReflectionViewModel(rounds, roundArgs)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { ReflectionScreen(onSaved = { saved = true }, viewModel = viewModel) }
            }
        }

        compose.onNodeWithText("Save reflection").performScrollTo().performClick()
        compose.onNodeWithText("Fill in at least one thing to save a reflection.").assertIsDisplayed()

        compose.onNodeWithContentDescription("Focus, 4 of 5").performScrollTo().performClick()
        compose.onNodeWithContentDescription("Focus, 4 of 5").assertIsSelected()
        compose.onNode(hasText("What worked") and hasSetTextAction()).performScrollTo().performTextInput("Tempo")
        compose.onNodeWithText("Save reflection").performScrollTo().performClick()

        compose.waitForIdle()
        assertEquals(
            "Tempo",
            rounds.added
                .single()
                .second.whatWorked,
        )
        assertTrue(saved)
    }
}
