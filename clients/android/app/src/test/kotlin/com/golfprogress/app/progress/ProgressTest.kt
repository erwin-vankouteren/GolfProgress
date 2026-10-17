package com.golfprogress.app.progress

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import com.golfprogress.app.testing.FakeProgressRepository
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

/** The Progress rules (#109): the web app's progress.ts. */
class ProgressRulesTest {
    private val today = LocalDate.of(2026, 10, 17)

    @Test
    fun `ranges count back whole months, to the month's last day when shorter`() {
        assertEquals(DateRange(from = LocalDate.of(2026, 7, 17)), rangeQuery(RangeKey.ThreeMonths, today, DateRange()))
        assertEquals(DateRange(from = LocalDate.of(2026, 2, 28)), rangeQuery(RangeKey.ThreeMonths, LocalDate.of(2026, 5, 31), DateRange()))
        assertEquals(DateRange(), rangeQuery(RangeKey.AllTime, today, DateRange()))
        assertFalse(DateRange(LocalDate.of(2026, 5, 2), LocalDate.of(2026, 5, 1)).isValid())
        assertTrue(DateRange(LocalDate.of(2026, 5, 1), null).isValid())
    }

    @Test
    fun `strokes are charted for 18-hole rounds when holes are mixed`() {
        val chart = timelineChart(Fixtures.timeline())

        assertEquals(18, chart.strokesHoles)
        assertEquals(listOf(0, 1, 3), chart.strokes.map { it.first })
        assertEquals(listOf(55, 60, 64, 70), chart.fitness.map { it.second })
        assertTrue(strokesLeaveOutRounds(Fixtures.timeline()))
    }

    @Test
    fun `summaries and the trend minimums`() {
        assertEquals(ScoreSummary(55, 70, 62), timelineSummary(Fixtures.timeline())?.score)
        assertNull(timelineSummary(emptyList()))
        assertEquals(2, roundsShortOfTrend(Fixtures.timeline().take(1)))
        assertEquals(MonthlySummary(2, MonthAverage("2026-08-01", 55), MonthAverage("2026-10-01", 67)), monthlySummary(Fixtures.months()))
        assertTrue(hasMonthlyTrend(Fixtures.months()))
        assertEquals("2026-10-01", defaultReviewMonth(Fixtures.months()))
    }

    @Test
    fun `the focus is trimmed and at most 200 characters`() {
        assertEquals(CheckedFocus.Ok("Fewer three-putts"), checkFocus("  Fewer three-putts "))
        assertEquals(CheckedFocus.Ok(null), checkFocus("   "))
        assertEquals(CheckedFocus.TooLong, checkFocus("x".repeat(201)))
        assertEquals(CheckedFocus.Ok("🏌".repeat(200)), checkFocus("🏌".repeat(200)))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class ProgressViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `loads everything and opens the latest month with rounds`() {
        val state = ProgressViewModel(FakeProgressRepository()).state.value

        assertEquals(Load.Loaded(4), state.roundCount)
        assertEquals(4, (state.timeline as Load.Loaded).value.size)
        assertEquals("2026-10-01", state.reviewMonth)
        assertEquals(Load.Loaded(null), state.review)
    }

    @Test
    fun `with no rounds nothing else is asked for`() {
        val repository = FakeProgressRepository(roundCount = 0)
        val state = ProgressViewModel(repository).state.value

        assertEquals(Load.Loaded(0), state.roundCount)
        assertTrue(repository.timelineQueries.isEmpty())
    }

    @Test
    fun `an impossible custom range isn't asked for`() {
        val repository = FakeProgressRepository()
        val viewModel = ProgressViewModel(repository)

        viewModel.setRange(RangeKey.Custom)
        viewModel.setCustom(DateRange(LocalDate.of(2026, 9, 1), LocalDate.of(2026, 8, 1)))

        assertFalse(viewModel.state.value.customValid)
        assertEquals(Pair<String?, String?>(null, null), repository.timelineQueries.last())
        viewModel.setCustom(DateRange(LocalDate.of(2026, 8, 1), LocalDate.of(2026, 9, 1)))
        assertEquals(Pair<String?, String?>("2026-08-01", "2026-09-01"), repository.timelineQueries.last())
    }

    @Test
    fun `creates a review with a trimmed focus`() {
        val repository = FakeProgressRepository()
        val viewModel = ProgressViewModel(repository)

        viewModel.setFocus("  Fewer three-putts ")
        viewModel.createReview()

        assertEquals("2026-10-01" to "Fewer three-putts", repository.created.single())
        assertTrue(viewModel.state.value.reviewReady)
        assertEquals(
            true,
            viewModel.state.value.monthly
                .let { it as Load.Loaded }
                .value
                .last()
                .hasReview,
        )
    }

    @Test
    fun `a month without rounds has nothing to review`() {
        val viewModel = ProgressViewModel(FakeProgressRepository())

        viewModel.chooseMonth("2026-09-01")

        assertTrue(viewModel.state.value.nothingToReview)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ProgressScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun show(repository: FakeProgressRepository = FakeProgressRepository()): FakeProgressRepository {
        val viewModel = ProgressViewModel(repository)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { ProgressScreen(onLogRound = {}, viewModel = viewModel) }
            }
        }
        return repository
    }

    @Test
    fun summarisesEachChart_AndShowsItAsATable() {
        show()

        compose.onNode(hasText("Rounds over time") and isHeading()).assertIsDisplayed()
        compose
            .onNodeWithText("4 rounds from Aug 2, 2026 to Oct 11, 2026. Fitness score went from 55 to 70, averaging 62.")
            .assertIsDisplayed()
        compose.onNodeWithText("The strokes line shows 18-hole rounds only; the table has every round.").assertIsDisplayed()

        compose.onAllNodesWithText("Show as table")[0].performScrollTo().performClick()
        compose.onNode(hasContentDescription("Date: Oct 11, 2026, Holes: 18, Strokes: 86, Fitness score: 70")).assertExists()

        compose
            .onNodeWithText("Scored rounds in 2 months. Average fitness score went from 55 in August 2026 to 67 in October 2026.")
            .performScrollTo()
            .assertIsDisplayed()
    }

    @Test
    fun showsTheBaseline() {
        show()

        compose.onNodeWithText("Based on your last 4 rounds.").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("88.3").assertExists()
        compose.onNodeWithText("40%").assertExists()
    }

    @Test
    fun createsTheLatestMonthsReview() {
        val repository = show()

        compose.onNodeWithText("There's no review of October 2026 yet. Create one to see how the month went.").performScrollTo()
        compose.onNode(hasText("Your focus for next month") and hasSetTextAction()).performScrollTo().performTextInput("Lag putting")
        compose.onNodeWithText("Create review").performScrollTo().performClick()

        compose.onNodeWithText("Review ready.").performScrollTo().assertIsDisplayed()
        compose.onNodeWithText("Logging consistently").assertExists()
        compose.onNode(hasContentDescription("Up 7 on the month before")).assertExists()
        assertEquals("2026-10-01" to "Lag putting", repository.created.single())
    }

    @Test
    fun lowData_SaysHowManyMoreRounds() {
        show(FakeProgressRepository(roundCount = 1, timeline = Fixtures.timeline().take(1)))

        compose.onNodeWithText("Choose a longer period", substring = true).assertDoesNotExist()
        compose.onNodeWithText("Log 2 more rounds to see a trend.").assertIsDisplayed()
    }
}
