package com.golfprogress.app.rounds

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.api.ApiProblem
import com.golfprogress.api.generated.models.FitnessScoreExplanation
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.testing.FakeRoundsRepository
import com.golfprogress.app.testing.FakeUserRepository
import com.golfprogress.app.testing.Fixtures
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LocalOnline
import com.golfprogress.app.ui.common.Pillar
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

/** What the detail says about a round (#97): the same as the web app's roundDetail.ts. */
class RoundDetailRulesTest {
    private fun score(
        average: Double? = 90.0,
        spread: Double? = 3.0,
        rounds: Int? = 3,
        practice: Int? = 1,
        reflected: Boolean? = false,
    ) = Fixtures.fitnessScore().copy(explanation = FitnessScoreExplanation(average, spread, rounds, practice, reflected))

    @Test
    fun `execution compares strokes per 18 with the recent average`() {
        val nine = Fixtures.savedRound(1, strokes = 44, holes = 9)
        assertEquals(
            listOf(Sentence.ComparedWithAverage(88, 90.0, -2.0), Sentence.NineHoleEquivalent(44, 88)),
            explainPillars(nine, score())[Pillar.Execution],
        )
        assertEquals(listOf(Sentence.FirstRound), explainPillars(nine, score(average = null))[Pillar.Execution])
    }

    @Test
    fun `damage control says what was tracked`() {
        assertEquals(listOf(Sentence.DamageNotTracked), explainPillars(Fixtures.savedRound(1), score())[Pillar.DamageControl])
        assertEquals(
            listOf(Sentence.Damage(2, null)),
            explainPillars(Fixtures.savedRound(1, penalties = 2), score())[Pillar.DamageControl],
        )
    }

    @Test
    fun `consistency needs a spread, and is full at or below the average`() {
        assertEquals(
            listOf(Sentence.ConsistencyNeedsRounds),
            explainPillars(Fixtures.savedRound(1), score(spread = null))[Pillar.Consistency],
        )
        assertEquals(
            listOf(Sentence.ConsistencyAtOrBetter),
            explainPillars(Fixtures.savedRound(1, strokes = 90), score())[Pillar.Consistency],
        )
        assertEquals(
            listOf(Sentence.ConsistencyWorse(4.0, 3.0)),
            explainPillars(Fixtures.savedRound(1, strokes = 94), score())[Pillar.Consistency],
        )
    }

    @Test
    fun `habits count rounds and practice, and say whether the round was reflected on`() {
        assertEquals(
            listOf(Sentence.Habits(3, 1), Sentence.NotReflected),
            explainPillars(Fixtures.savedRound(1), score())[Pillar.Habits],
        )
        assertEquals(listOf(Sentence.HabitsUnknown), explainPillars(Fixtures.savedRound(1), score(rounds = null))[Pillar.Habits])
    }

    @Test
    fun `editing keeps every stat the round has, whatever the mode`() {
        val round = Fixtures.savedRound(1, penalties = 1, blowUps = 2)
        assertEquals(listOf(StatField.Penalties, StatField.BlowUpHoles), statFieldsForEdit(UserResponse.TrackingMode.Minimal, round))
        assertEquals("1", formValuesFrom(round).stat(StatField.Penalties))
        assertEquals("", formValuesFrom(round).stat(StatField.Gir))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class RoundsViewModelsTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun rounds(count: Int) = (1..count).map { Fixtures.savedRound(it) }

    @Test
    fun `the list pages until the total`() {
        val repository = FakeRoundsRepository(rounds(45))
        val viewModel = RoundsListViewModel(repository)
        assertEquals(20, viewModel.state.value.rounds.size)

        viewModel.loadMore()
        viewModel.loadMore()
        viewModel.loadMore()

        assertEquals(45, viewModel.state.value.rounds.size)
        assertEquals(false, viewModel.state.value.hasMore)
        assertEquals(listOf(0 to 20, 20 to 20, 40 to 20), repository.pages)
    }

    @Test
    fun `deleting from the list asks first, then removes the round`() {
        val repository = FakeRoundsRepository(rounds(3))
        val viewModel = RoundsListViewModel(repository)
        val second = viewModel.state.value.rounds[1]

        viewModel.askToDelete(second)
        assertEquals(second, viewModel.state.value.confirmDelete)
        viewModel.confirmDelete()

        assertEquals(listOf(second.roundId), repository.deleted)
        assertEquals(2, viewModel.state.value.rounds.size)
        assertNull(viewModel.state.value.confirmDelete)
    }

    @Test
    fun `a failed delete keeps the dialog open and says so`() {
        val repository = FakeRoundsRepository(rounds(1))
        val viewModel = RoundsListViewModel(repository)
        viewModel.askToDelete(viewModel.state.value.rounds[0])

        repository.failWith = IOException()
        viewModel.confirmDelete()

        assertTrue(viewModel.state.value.deleteFailed)
        assertEquals(1, viewModel.state.value.rounds.size)
    }

    @Test
    fun `the detail loads everything, or says the round is gone`() {
        val repository = FakeRoundsRepository(rounds(1)).apply { reflections = listOf(Fixtures.reflection()) }

        val found = RoundDetailViewModel(repository, roundArgs(Fixtures.roundId(1))).state.value.detail
        val detail = (found as Load.Loaded).value!!
        assertEquals(88, detail.round.totalStrokes)
        assertEquals("Cut down three-putts", detail.nextLever?.title)
        assertEquals(1, detail.reflections.size)

        val gone = RoundDetailViewModel(repository, roundArgs(Fixtures.roundId(7))).state.value.detail
        assertEquals(Load.Loaded(null), gone)
    }

    @Test
    fun `deleting from the detail finishes it, even when it's already gone`() {
        val repository = FakeRoundsRepository(rounds(1))
        val viewModel = RoundDetailViewModel(repository, roundArgs(Fixtures.roundId(1)))

        repository.failWith = ApiProblem(404)
        viewModel.askToDelete()
        viewModel.confirmDelete()

        assertTrue(viewModel.state.value.deleted)
    }

    @Test
    fun `editing prefills the form and saves with PUT`() {
        val repository = FakeRoundsRepository(listOf(Fixtures.savedRound(1, strokes = 91, penalties = 2)))
        val viewModel =
            RoundFormViewModel(
                FakeUserRepository(me = { Fixtures.user(trackingMode = UserResponse.TrackingMode.Minimal) }),
                repository,
                roundArgs(Fixtures.roundId(1)),
            )
        assertEquals("91", viewModel.state.value.values.totalStrokes)
        // Minimal mode, but the round tracked penalties: they stay in the form.
        assertEquals(listOf(StatField.Penalties), viewModel.state.value.stats)

        viewModel.setStrokes("89")
        viewModel.save()

        val (id, request) = repository.updated.single()
        assertEquals(Fixtures.roundId(1), id)
        assertEquals(89, request.totalStrokes)
        assertEquals(2, request.penalties)
        assertTrue(repository.created.isEmpty())
    }

    @Test
    fun `editing a round that no longer exists says so`() {
        val viewModel = RoundFormViewModel(FakeUserRepository(), FakeRoundsRepository(), roundArgs(Fixtures.roundId(5)))

        assertTrue(viewModel.state.value.gone)
    }
}

private fun roundArgs(id: UUID) = SavedStateHandle(mapOf(RoundFormViewModel.ROUND_ID to id.toString()))

/** The list and the detail on screen (#97). */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class RoundsScreensTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private var opened: UUID? = null

    private fun showList(repository: FakeRoundsRepository): RoundsListViewModel {
        val viewModel = RoundsListViewModel(repository)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { RoundsListScreen(onOpenRound = { opened = it }, onLogRound = {}, viewModel = viewModel) }
            }
        }
        return viewModel
    }

    private fun showDetail(repository: FakeRoundsRepository) {
        val viewModel = RoundDetailViewModel(repository, roundArgs(Fixtures.roundId(1)))
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { RoundDetailScreen(onEdit = {}, onDeleted = {}, viewModel = viewModel) }
            }
        }
    }

    @Test
    fun theListReadsEachRoundAsOneItem_AndOpensIt() {
        showList(FakeRoundsRepository(listOf(Fixtures.savedRound(1, strokes = 91))))

        val row = compose.onNode(hasContentDescription("May 19, 2026, Hilversum, 91 strokes, Fitness score 72"))
        row.assertIsDisplayed()
        row.performClick()
        assertEquals(Fixtures.roundId(1), opened)
    }

    @Test
    fun theListOffersDeleteToTalkBack_AndAsksFirst() {
        val repository = FakeRoundsRepository(listOf(Fixtures.savedRound(1)))
        showList(repository)

        val row = compose.onNode(hasContentDescription("Hilversum", substring = true)).fetchSemanticsNode()
        val delete = row.config[SemanticsActions.CustomActions].single()
        assertEquals("Delete round", delete.label)
        compose.runOnIdle { delete.action() }

        compose.onNodeWithText("Delete this round?").assertIsDisplayed()
        compose.onNode(hasText("Delete round") and SemanticsMatcher.keyIsDefined(SemanticsActions.OnClick)).performClick()

        assertEquals(listOf(Fixtures.roundId(1)), repository.deleted)
        compose.onNodeWithText("No rounds logged yet.", substring = true).assertIsDisplayed()
    }

    @Test
    fun theDetailExplainsAPillarWhenOpened() {
        showDetail(FakeRoundsRepository(listOf(Fixtures.savedRound(1, penalties = 1))))

        compose.onNodeWithContentDescription("Fitness score 72").assertIsDisplayed()
        compose.onNode(hasText("Fitness score") and isHeading()).assertIsDisplayed()
        compose.onNodeWithText("1 penalty, blow-up holes not tracked. Fewer of either scores higher.").assertDoesNotExist()

        compose.onNodeWithText("Damage control").performScrollTo().performClick()

        compose.onNodeWithText("1 penalty, blow-up holes not tracked. Fewer of either scores higher.").assertIsDisplayed()
    }

    @Test
    fun theDetailShowsStatsAndReflections() {
        val repository =
            FakeRoundsRepository(listOf(Fixtures.savedRound(1, putts = 33))).apply { reflections = listOf(Fixtures.reflection()) }
        showDetail(repository)

        compose.onNodeWithText("33").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Tempo on the tee").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("4 of 5").assertExists()
    }

    @Test
    fun aDeletedRoundSaysSo() {
        val repository = FakeRoundsRepository(emptyList())
        showDetail(repository)

        compose.onNodeWithText("This round no longer exists. It may have been deleted on another device.").assertIsDisplayed()
    }
}
