package com.golfprogress.app.dashboard

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasScrollAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToNode
import androidx.compose.ui.unit.Density
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.testing.FakeDashboardRepository
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
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException
import java.util.UUID

/** Home's view model (#90): loading, failures, refreshing, and the last round's pillars. */
@OptIn(ExperimentalCoroutinesApi::class)
class DashboardViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `loads the dashboard and the last round's pillars`() {
        val viewModel = DashboardViewModel(FakeDashboardRepository())

        val state = viewModel.state.value
        assertEquals(Fixtures.dashboard(), (state.dashboard as Load.Loaded).value)
        assertEquals(18, state.pillars?.execution)
    }

    @Test
    fun `a new user has no pillars to load`() {
        val viewModel = DashboardViewModel(FakeDashboardRepository(dashboard = { Fixtures.emptyDashboard() }))

        assertNull(viewModel.state.value.pillars)
    }

    @Test
    fun `a failure can be retried`() {
        var fail = true
        val repository = FakeDashboardRepository(dashboard = { if (fail) throw IOException("offline") else Fixtures.dashboard() })
        val viewModel = DashboardViewModel(repository)
        assertTrue(viewModel.state.value.dashboard is Load.Failed)

        fail = false
        viewModel.retry()

        assertTrue(viewModel.state.value.dashboard is Load.Loaded)
    }

    @Test
    fun `a failed refresh keeps what's on screen`() {
        var fail = false
        val repository = FakeDashboardRepository(dashboard = { if (fail) throw ApiProblem(500) else Fixtures.dashboard() })
        val viewModel = DashboardViewModel(repository)

        fail = true
        viewModel.refresh()

        val state = viewModel.state.value
        assertTrue(state.dashboard is Load.Loaded)
        assertEquals(false, state.refreshing)
        assertEquals(2, repository.dashboardCalls)
    }
}

/** Home's screen (#90): its cards, states and accessibility, on Robolectric. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class DashboardScreenTest {
    @get:Rule
    val compose = createComposeRule()

    private var retried = 0
    private var loggedRound = 0
    private var opened: UUID? = null

    private fun show(
        state: DashboardState,
        fontScale: Float = 1f,
    ) {
        compose.setContent {
            val density = LocalDensity.current
            CompositionLocalProvider(LocalDensity provides Density(density.density, fontScale)) {
                GolfProgressTheme {
                    DashboardContent(
                        state = state,
                        onRetry = { retried++ },
                        onRefresh = {},
                        onLogRound = { loggedRound++ },
                        onOpenRound = { opened = it },
                    )
                }
            }
        }
    }

    private fun loaded() = DashboardState(Load.Loaded(Fixtures.dashboard()), pillars = Fixtures.fitnessScore())

    private fun scrollTo(text: String) {
        compose.onNode(hasScrollAction()).performScrollToNode(hasText(text))
    }

    @Test
    fun showsTheLastRoundWithItsScoreAndPillars() {
        show(loaded())

        compose.onNode(hasText("Last round") and isHeading()).assertIsDisplayed()
        compose.onNodeWithContentDescription("Fitness score 72").assertIsDisplayed()
        compose.onNodeWithContentDescription("Confidence: Medium").assertIsDisplayed()
        compose.onNodeWithText("Execution").assertIsDisplayed()
        compose.onNodeWithText("18 / 25").assertIsDisplayed()

        compose.onNodeWithText("May 3, 2026 · 88 strokes").performClick()
        assertEquals(Fixtures.roundId(1), opened)
    }

    @Test
    fun showsTheNextFocusBaselineMicroWinsAndThisMonth() {
        show(loaded())

        scrollTo("Cut down three-putts")
        compose.onNode(hasText("Your next focus") and isHeading()).assertIsDisplayed()
        scrollTo("91.4")
        compose.onNodeWithContentDescription("Confidence: Low").assertIsDisplayed()
        compose.onNodeWithText("Based on only a few rounds so far: early days.").assertIsDisplayed()
        scrollTo("✓ Fewer penalties")
        compose.onNode(hasScrollAction()).performScrollToNode(hasContentDescription("Up 4 on last month"))
        compose.onNodeWithContentDescription("Up 4 on last month").assertIsDisplayed()
    }

    @Test
    fun leavesOutWhatANewUserDoesNotHave_AndOffersTheFirstRound() {
        show(DashboardState(Load.Loaded(Fixtures.emptyDashboard())))

        compose.onNodeWithText("Log your first round to see your fitness score.").assertIsDisplayed()
        compose.onNodeWithText("Last round").assertDoesNotExist()
        compose.onNodeWithText("Log your first round").performClick()
        assertEquals(1, loggedRound)
    }

    @Test
    fun saysWhenAScoreIsMissing() {
        show(DashboardState(Load.Loaded(Fixtures.dashboard(lastRound = Fixtures.lastRound(score = null, confidence = null)))))

        compose.onNodeWithText("This round hasn't been scored yet.").assertIsDisplayed()
    }

    @Test
    fun loadsWithASkeleton_AndRetriesAfterAnError() {
        show(DashboardState(Load.Loading))
        compose.onNodeWithContentDescription("Loading…").assertIsDisplayed()
    }

    @Test
    fun anErrorOffersTryAgain() {
        show(DashboardState(Load.Failed(IOException())))

        compose.onNodeWithText("Couldn't load your dashboard.").assertIsDisplayed()
        compose.onNodeWithText("Try again").performClick()
        assertEquals(1, retried)
    }

    @Test
    fun worksAtTwiceTheFontSize() {
        show(loaded(), fontScale = 2f)

        compose.onNode(hasText("Last round") and isHeading()).assertIsDisplayed()
        compose.onNodeWithContentDescription("Fitness score 72").assertIsDisplayed()
        compose.onNode(hasScrollAction()).performScrollToNode(hasContentDescription("Up 4 on last month"))
    }
}
