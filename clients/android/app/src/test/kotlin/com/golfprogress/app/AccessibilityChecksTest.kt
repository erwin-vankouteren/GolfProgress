package com.golfprogress.app

import androidx.activity.ComponentActivity
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import com.golfprogress.app.dashboard.DashboardContent
import com.golfprogress.app.dashboard.DashboardState
import com.golfprogress.app.practice.LogPracticeScreen
import com.golfprogress.app.practice.LogPracticeViewModel
import com.golfprogress.app.practice.PracticeListScreen
import com.golfprogress.app.practice.PracticeListViewModel
import com.golfprogress.app.profile.OnboardingScreen
import com.golfprogress.app.profile.OnboardingViewModel
import com.golfprogress.app.profile.ProfileScreen
import com.golfprogress.app.profile.ProfileViewModel
import com.golfprogress.app.progress.ProgressScreen
import com.golfprogress.app.progress.ProgressViewModel
import com.golfprogress.app.rounds.ReflectionScreen
import com.golfprogress.app.rounds.ReflectionViewModel
import com.golfprogress.app.rounds.RoundDetailScreen
import com.golfprogress.app.rounds.RoundDetailViewModel
import com.golfprogress.app.rounds.RoundFormScreen
import com.golfprogress.app.rounds.RoundFormViewModel
import com.golfprogress.app.rounds.RoundsListScreen
import com.golfprogress.app.rounds.RoundsListViewModel
import com.golfprogress.app.testing.FakePracticeRepository
import com.golfprogress.app.testing.FakeProgressRepository
import com.golfprogress.app.testing.FakeRoundsRepository
import com.golfprogress.app.testing.FakeUserRepository
import com.golfprogress.app.testing.Fixtures
import com.golfprogress.app.testing.assertControlsAccessible
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LocalOnline
import com.golfprogress.app.ui.theme.GolfProgressTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertThrows
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/**
 * The accessibility audit's automated part on Android (#120): each main screen, with
 * content, in light and dark, has a name for TalkBack and a 48 dp touch target on every
 * control (testing/AccessibilityAssertions.kt). The Accessibility Test Framework
 * can't see Compose's tree under Robolectric, so these walk the semantics tree
 * instead. TalkBack itself still needs checking by hand on a device.
 */
@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class AccessibilityChecksTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    @Before
    fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
    }

    @After
    fun reset() = Dispatchers.resetMain()

    private val roundArgs = SavedStateHandle(mapOf(RoundFormViewModel.ROUND_ID to Fixtures.roundId(1).toString()))

    private fun check(
        dark: Boolean = false,
        screen: @Composable () -> Unit,
    ) {
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme(darkTheme = dark) { screen() }
            }
        }
        compose.onRoot().assertControlsAccessible(compose.density)
    }

    /** The control: the checks do catch a problem, so the screens passing means something. */
    @Test
    fun theChecksCatchATinyUnlabelledButton() {
        assertThrows(Throwable::class.java) {
            check { Box(Modifier.size(10.dp).clickable {}) }
        }
    }

    private fun oneRound() = FakeRoundsRepository(listOf(Fixtures.savedRound(1, penalties = 1, putts = 33)))

    @Test
    fun home() = check { DashboardContent(DashboardState(Load.Loaded(Fixtures.dashboard()), Fixtures.fitnessScore()), {}, {}, {}, {}) }

    @Test
    fun homeDark() =
        check(dark = true) {
            DashboardContent(DashboardState(Load.Loaded(Fixtures.dashboard()), Fixtures.fitnessScore()), {}, {}, {}, {})
        }

    @Test
    fun rounds() {
        val viewModel = RoundsListViewModel(oneRound())
        check { RoundsListScreen(onOpenRound = {}, onLogRound = {}, viewModel = viewModel) }
    }

    @Test
    fun roundDetail() {
        val viewModel = RoundDetailViewModel(oneRound(), roundArgs)
        check { RoundDetailScreen(onEdit = {}, onDeleted = {}, onAddReflection = {}, viewModel = viewModel) }
    }

    @Test
    fun logRound() {
        val viewModel = RoundFormViewModel(FakeUserRepository(), FakeRoundsRepository(), SavedStateHandle())
        check { RoundFormScreen(onSaved = {}, viewModel = viewModel) }
    }

    @Test
    fun reflection() {
        val viewModel = ReflectionViewModel(FakeRoundsRepository(), roundArgs)
        check { ReflectionScreen(onSaved = {}, viewModel = viewModel) }
    }

    @Test
    fun practice() {
        val viewModel = PracticeListViewModel(FakePracticeRepository(listOf(Fixtures.practiceSession(1))))
        check { PracticeListScreen(onOpen = {}, onLogPractice = {}, viewModel = viewModel) }
    }

    @Test
    fun logPractice() {
        val viewModel = LogPracticeViewModel(FakePracticeRepository())
        check { LogPracticeScreen(onSaved = {}, viewModel = viewModel) }
    }

    @Test
    fun progress() {
        val viewModel = ProgressViewModel(FakeProgressRepository())
        check { ProgressScreen(onLogRound = {}, viewModel = viewModel) }
    }

    @Test
    fun progressDark() {
        val viewModel = ProgressViewModel(FakeProgressRepository())
        check(dark = true) { ProgressScreen(onLogRound = {}, viewModel = viewModel) }
    }

    @Test
    fun profile() {
        val viewModel = ProfileViewModel(FakeUserRepository())
        check { ProfileScreen(onSignOut = {}, viewModel = viewModel) }
    }

    @Test
    fun onboarding() {
        val viewModel = OnboardingViewModel(FakeUserRepository())
        check { OnboardingScreen(onFinished = {}, onSkip = {}, viewModel = viewModel) }
    }
}
