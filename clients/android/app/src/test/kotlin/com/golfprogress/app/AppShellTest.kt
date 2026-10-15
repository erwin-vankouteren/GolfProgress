package com.golfprogress.app

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.Density
import com.golfprogress.app.navigation.MainScreen
import com.golfprogress.app.ui.theme.GolfProgressTheme
import com.golfprogress.app.version.MinimumVersionSource
import com.golfprogress.app.version.UpdateRequiredScreen
import com.golfprogress.app.version.VersionState
import com.golfprogress.app.version.VersionViewModel
import com.golfprogress.app.version.compareVersions
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

/** The app shell (#77), on the JVM with Robolectric: no emulator needed. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class MainScreenTest {
    @get:Rule
    val compose = createComposeRule()

    private fun heading(text: String) = compose.onNode(hasText(text) and isHeading())

    /** A bottom-bar item: Material gives them the tab role, which TalkBack announces. */
    private fun tab(text: String) = compose.onNode(hasText(text) and SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.Tab))

    @Before
    fun show() {
        compose.setContent { GolfProgressTheme { MainScreen() } }
    }

    @Test
    fun startsOnHomeWithTheTabs() {
        heading("Home").assertIsDisplayed()
        listOf("Home", "Rounds", "Log round", "Practice", "Progress").forEach { tab(it).assertIsDisplayed() }
    }

    @Test
    fun movesBetweenTabs() {
        tab("Rounds").performClick()
        heading("Rounds").assertIsDisplayed()

        tab("Progress").performClick()
        heading("Progress").assertIsDisplayed()
    }

    @Test
    fun logRoundOpensFromTheCentreButtonAndGoesBack() {
        tab("Rounds").performClick()
        tab("Log round").performClick()
        heading("Log round").assertIsDisplayed()

        compose.onNodeWithContentDescription("Back").performClick()
        heading("Rounds").assertIsDisplayed()
    }

    @Test
    fun profileOpensFromTheTopBarAvatar() {
        compose.onNodeWithContentDescription("Profile").performClick()

        heading("Profile").assertIsDisplayed()
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class UpdateRequiredScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun explainsTheUpdate() {
        compose.setContent { GolfProgressTheme { UpdateRequiredScreen() } }

        compose.onNode(hasText("Update required") and isHeading()).assertIsDisplayed()
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class VersionViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun stateFor(
        appVersion: String,
        source: MinimumVersionSource,
    ) = VersionViewModel(source, appVersion).state.value

    @Test
    fun olderThanTheMinimumRequiresAnUpdate() {
        assertEquals(VersionState.UpdateRequired, stateFor("1.0.0") { "1.2.0" })
    }

    @Test
    fun meetingTheMinimumIsSupported() {
        assertEquals(VersionState.Supported, stateFor("1.2.0") { "1.2.0" })
    }

    @Test
    fun aFailedCheckNeverBlocksTheApp() {
        assertEquals(VersionState.Supported, stateFor("1.0.0") { error("offline") })
    }

    @Test
    fun comparesVersionsNumerically() {
        assertEquals(0, compareVersions("1.0.0", "1.0.0"))
        assertEquals(true, compareVersions("1.0.0", "1.0.1") < 0)
        assertEquals(true, compareVersions("1.10.0", "1.9.0") > 0)
        assertEquals(0, compareVersions("1.0", "1.0.0"))
    }
}

/**
 * Accessibility checks (#78): at 200% font scale the shell keeps its heading and every
 * bottom-bar item on screen, and icon-only controls have a label TalkBack can read.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class LargeFontScaleTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun theShellWorksAtTwiceTheFontSize() {
        compose.setContent {
            val density = LocalDensity.current
            CompositionLocalProvider(LocalDensity provides Density(density.density, fontScale = 2f)) {
                GolfProgressTheme { MainScreen() }
            }
        }

        compose.onNode(hasText("Home") and isHeading()).assertIsDisplayed()
        listOf("Home", "Rounds", "Log round", "Practice", "Progress").forEach {
            compose
                .onNode(hasText(it) and SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.Tab))
                .assertIsDisplayed()
        }
        compose.onNodeWithContentDescription("Profile").assertIsDisplayed().assertHasClickAction()
    }
}
