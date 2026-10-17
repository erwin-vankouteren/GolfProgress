package com.golfprogress.app.rounds

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.testing.FakeRoundsRepository
import com.golfprogress.app.testing.FakeUserRepository
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
import java.io.IOException
import java.time.LocalDate
import java.util.UUID

/** The form's rules (#94): the same as the web app's roundForm.ts and the API's. */
class RoundFormRulesTest {
    private val today = LocalDate.of(2026, 5, 3)

    private fun values(
        strokes: String = "88",
        holes: Int = 18,
        vararg stats: Pair<StatField, String>,
    ) = RoundFormValues(playedAt = today, holes = holes, totalStrokes = strokes, stats = stats.toMap())

    @Test
    fun `the tracking mode decides the stats`() {
        assertEquals(emptyList<StatField>(), statFieldsFor(UserResponse.TrackingMode.Minimal))
        assertEquals(listOf(StatField.TotalPutts, StatField.Penalties), statFieldsFor(UserResponse.TrackingMode.Balanced))
        assertEquals(StatField.entries, statFieldsFor(UserResponse.TrackingMode.Detailed))
        // Unknown: show the most, so nothing is hidden.
        assertEquals(StatField.entries, statFieldsFor(null))
    }

    @Test
    fun `strokes are required, whole and above zero`() {
        assertEquals(RoundError.StrokesRequired, validateRound(values(strokes = " "), emptyList())[RoundField.TotalStrokes])
        assertEquals(RoundError.WholeNumber, validateRound(values(strokes = "8.5"), emptyList())[RoundField.TotalStrokes])
        assertEquals(RoundError.StrokesPositive, validateRound(values(strokes = "0"), emptyList())[RoundField.TotalStrokes])
        assertEquals(emptyMap<RoundField, RoundError>(), validateRound(values(), emptyList()))
    }

    @Test
    fun `stats are capped by holes or by strokes`() {
        val all = StatField.entries
        assertEquals(
            RoundError.AtMostHoles(9),
            validateRound(values("45", 9, StatField.FairwaysHit to "10"), all)[RoundField.FairwaysHit],
        )
        assertEquals(
            RoundError.AtMostStrokes(70),
            validateRound(values("70", 18, StatField.TotalPutts to "71"), all)[RoundField.TotalPutts],
        )
        assertEquals(RoundError.NotNegative, validateRound(values("70", 18, StatField.Gir to "-1"), all)[RoundField.Gir])
        // Empty means not tracked this round.
        assertEquals(emptyMap<RoundField, RoundError>(), validateRound(values("70", 18, StatField.Gir to ""), all))
    }

    @Test
    fun `the request leaves out what isn't shown or filled in`() {
        val request =
            toRequest(
                values("88", 18, StatField.TotalPutts to "33", StatField.Gir to "6").copy(courseName = " Hilversum ", notes = " "),
                listOf(StatField.TotalPutts, StatField.Penalties),
            )

        assertEquals("2026-05-03", request.playedAt)
        assertEquals(88, request.totalStrokes)
        assertEquals("Hilversum", request.courseName)
        assertEquals(33, request.totalPutts)
        assertNull(request.penalties)
        assertNull(request.gir) // not shown in this mode
        assertNull(request.notes)
    }

    @Test
    fun `steppers start somewhere useful and stay in range`() {
        assertEquals("92", stepStrokes(values(strokes = ""), 1, typical18Score = 92))
        assertEquals("45", stepStrokes(values(strokes = "", holes = 9), 1, typical18Score = null))
        assertEquals("1", stepStrokes(values(strokes = "1"), -1, null))
        assertEquals("36", stepStat(StatField.TotalPutts, values(), 1))
        assertEquals("18", stepStat(StatField.Gir, values("80", 18, StatField.Gir to "18"), 1))
        assertEquals("0", stepStat(StatField.Penalties, values("80", 18, StatField.Penalties to "0"), -1))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class RoundFormViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `saves the round, then hands over its id`() {
        val rounds = FakeRoundsRepository()
        val viewModel = RoundFormViewModel(FakeUserRepository(), rounds, SavedStateHandle())

        viewModel.setStrokes("88")
        viewModel.setStat(StatField.TotalPutts, "33")
        viewModel.save()

        assertEquals(88, rounds.created.single().totalStrokes)
        assertEquals(33, rounds.created.single().totalPutts)
        assertEquals(Fixtures.roundId(9), viewModel.state.value.savedRoundId)
        // Saved once: a second press does nothing.
        viewModel.save()
        assertEquals(1, rounds.created.size)
    }

    @Test
    fun `refuses to send a wrong form, and asks for focus on the first wrong field`() {
        val rounds = FakeRoundsRepository()
        val viewModel = RoundFormViewModel(FakeUserRepository(), rounds, SavedStateHandle())

        viewModel.save()

        assertEquals(RoundField.TotalStrokes, viewModel.state.value.focus)
        assertEquals(FieldMessage.Rule(RoundError.StrokesRequired), viewModel.state.value.messageFor(RoundField.TotalStrokes))
        assertTrue(rounds.created.isEmpty())
    }

    @Test
    fun `shows the API's 400 messages next to their fields`() {
        val rounds =
            FakeRoundsRepository(respond = {
                throw ApiProblem(400, errors = mapOf("PlayedAt" to listOf("PlayedAt can't be in the future.")))
            })
        val viewModel = RoundFormViewModel(FakeUserRepository(), rounds, SavedStateHandle())

        viewModel.setStrokes("88")
        viewModel.save()

        assertEquals(FieldMessage.FromApi("PlayedAt can't be in the future."), viewModel.state.value.messageFor(RoundField.PlayedAt))
        assertNull(viewModel.state.value.savedRoundId)
    }

    @Test
    fun `a failure without field errors says so`() {
        val viewModel =
            RoundFormViewModel(FakeUserRepository(), FakeRoundsRepository(respond = { throw IOException() }), SavedStateHandle())

        viewModel.setStrokes("88")
        viewModel.save()

        assertEquals(FormProblem.CantConnect, viewModel.state.value.problem)
        assertEquals(false, viewModel.state.value.saving)
    }

    @Test
    fun `without the profile there's nothing to save yet, and Try again reloads it`() {
        var fail = true
        val viewModel =
            RoundFormViewModel(
                FakeUserRepository(me = {
                    if (fail) throw IOException() else Fixtures.user()
                }),
                FakeRoundsRepository(),
                SavedStateHandle(),
            )
        assertTrue(viewModel.state.value.me is Load.Failed)

        fail = false
        viewModel.retry()

        assertTrue(viewModel.state.value.me is Load.Loaded)
    }
}

/** The form on screen (#94): the main flow, mode-dependent fields and the steppers. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class RoundFormScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private var saved: UUID? = null

    private fun show(
        mode: UserResponse.TrackingMode = UserResponse.TrackingMode.Balanced,
        rounds: FakeRoundsRepository = FakeRoundsRepository(),
        online: Boolean = true,
    ): FakeRoundsRepository {
        val viewModel = RoundFormViewModel(FakeUserRepository(me = { Fixtures.user(trackingMode = mode) }), rounds, SavedStateHandle())
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides online) {
                GolfProgressTheme { RoundFormScreen(onSaved = { saved = it }, viewModel = viewModel) }
            }
        }
        return rounds
    }

    @Test
    fun offline_SaveIsDisabled_AndSaysWhy() {
        show(online = false)

        compose.onNodeWithText("You're offline. Connect to save this round.").performScrollTo().assertIsDisplayed()
        compose.onNode(hasText("Save") and hasClickAction()).performScrollTo().assertIsNotEnabled()
    }

    private fun field(label: String) = compose.onNode(hasText(label) and hasSetTextAction())

    @Test
    fun logsARoundInMinimalMode_WithOnlyTheStrokes() {
        val rounds = show(UserResponse.TrackingMode.Minimal)

        compose.onNodeWithText("Putts").assertDoesNotExist()
        field("Strokes").performTextInput("88")
        compose.onNodeWithText("Save").performScrollTo().performClick()

        assertEquals(88, rounds.created.single().totalStrokes)
        compose.waitForIdle()
        assertEquals(Fixtures.roundId(9), saved)
    }

    @Test
    fun balancedShowsPuttsAndPenalties_WithTheirLimit() {
        show(UserResponse.TrackingMode.Balanced)

        field("Strokes").performTextInput("80")
        compose.onNodeWithText("Putts").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Penalties").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Fairways hit").assertDoesNotExist()
        compose.onNodeWithText("More stats? Change your tracking mode in Profile.").performScrollTo().assertIsDisplayed()

        field("Putts").performTextInput("81")
        compose.onNodeWithText("Can't be more than your strokes (80).").assertIsDisplayed()
    }

    @Test
    fun steppersAreLabelled_AndStartFromTheTypicalScore() {
        show(UserResponse.TrackingMode.Detailed)

        compose.onNodeWithContentDescription("Increase Strokes").performClick()
        compose.onNodeWithText("92").assertIsDisplayed()
        compose.onNodeWithContentDescription("Increase Greens in regulation").performScrollTo().performClick()
        // Fairways, greens, three-putts and blow-ups are each capped at the hole count.
        assertEquals(4, compose.onAllNodesWithText("out of 18").fetchSemanticsNodes().size)
    }

    @Test
    fun saveWithoutStrokes_ExplainsAndFocusesTheField() {
        val rounds = show()

        compose.onNodeWithText("Save").performScrollTo().performClick()

        compose.onNodeWithText("Enter your strokes.").performScrollTo().assertIsDisplayed()
        field("Strokes").assertIsFocused()
        assertTrue(rounds.created.isEmpty())
    }
}
