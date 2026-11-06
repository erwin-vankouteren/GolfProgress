package com.golfprogress.app.coaching

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.testing.FakeCoachingRepository
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

/** The rules of inviting a coach (#186): the web app's coachForm.ts. */
class CoachRulesTest {
    @Test
    fun `an address of the right shape is needed`() {
        assertEquals(CoachEmailError.Required, checkCoachEmail("  "))
        assertEquals(CoachEmailError.Invalid, checkCoachEmail("coach@"))
        assertNull(checkCoachEmail(" coach@example.test "))
    }

    @Test
    fun `rounds and progress are always shared`() {
        assertEquals(listOf(SharedItem.Rounds), sharedItems(shareReflections = false, sharePractice = false))
        assertEquals(
            listOf(SharedItem.Rounds, SharedItem.Reflections, SharedItem.Practice),
            sharedItems(shareReflections = true, sharePractice = true),
        )
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class CoachesViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `sending invites with what was chosen, then lists the invitation and clears the form`() {
        val coaching = FakeCoachingRepository()
        val viewModel = CoachesViewModel(coaching)

        viewModel.setEmail(" pro@example.test ")
        viewModel.setShareReflections(false)
        viewModel.send()

        assertEquals(listOf(Triple("pro@example.test", false, true)), coaching.invited)
        val state = viewModel.state.value
        assertEquals(SentInvitation("pro@example.test", resent = false), state.sent)
        assertEquals("", state.email)
        assertTrue(state.shareReflections)
        val loaded = state.coaches as Load.Loaded
        assertEquals(listOf("pro@example.test"), loaded.value.invitations.map { it.email })
    }

    @Test
    fun `an invalid address isn't sent`() {
        val coaching = FakeCoachingRepository()
        val viewModel = CoachesViewModel(coaching)

        viewModel.send()

        assertEquals(CoachEmailError.Required, viewModel.state.value.emailError)
        assertTrue(coaching.invited.isEmpty())
    }

    @Test
    fun `the API's refusal shows on the address, and too many today on the form`() {
        val coaching = FakeCoachingRepository()
        val viewModel = CoachesViewModel(coaching)
        viewModel.setEmail("pro@example.test")

        coaching.failWith = ApiProblem(400, errors = mapOf("Email" to listOf("This person is already your coach.")))
        viewModel.send()
        assertEquals("This person is already your coach.", viewModel.state.value.emailApiError)
        assertNull(viewModel.state.value.inviteProblem)

        coaching.failWith = ApiProblem(429)
        viewModel.send()
        assertEquals(InviteProblem.TooManyToday, viewModel.state.value.inviteProblem)

        coaching.failWith = IOException("offline")
        viewModel.send()
        assertEquals(InviteProblem.CantConnect, viewModel.state.value.inviteProblem)
    }

    @Test
    fun `cancelling removes the invitation`() {
        val invitation = Fixtures.coachInvitation()
        val coaching = FakeCoachingRepository(invitations = mutableListOf(invitation))
        val viewModel = CoachesViewModel(coaching)

        viewModel.cancel(invitation.coachInvitationId)

        assertTrue((viewModel.state.value.coaches as Load.Loaded).value.invitations.isEmpty())
    }

    @Test
    fun `an invitation that's gone meanwhile refreshes the list, without an error`() {
        val invitation = Fixtures.coachInvitation()
        val coaching = FakeCoachingRepository(invitations = mutableListOf(invitation))
        val viewModel = CoachesViewModel(coaching)
        coaching.invitations.clear()
        coaching.failWith = ApiProblem(404)

        viewModel.cancel(invitation.coachInvitationId)

        assertNull(viewModel.state.value.cancelProblem)
        assertTrue((viewModel.state.value.coaches as Load.Loaded).value.invitations.isEmpty())
    }

    @Test
    fun `a list that can't load can be tried again`() {
        val coaching = FakeCoachingRepository(listFailsWith = IOException("offline"))
        val viewModel = CoachesViewModel(coaching)
        assertTrue(viewModel.state.value.coaches is Load.Failed)

        coaching.listFailsWith = null
        viewModel.retry()

        assertTrue(viewModel.state.value.coaches is Load.Loaded)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class CoachesScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private fun show(
        coaching: FakeCoachingRepository,
        online: Boolean = true,
    ) {
        val viewModel = CoachesViewModel(coaching)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides online) {
                GolfProgressTheme { CoachesScreen(viewModel = viewModel) }
            }
        }
    }

    @Test
    fun `invites a coach, and shows the invitation with Cancel`() {
        val coaching = FakeCoachingRepository()
        show(coaching)
        compose.onNodeWithText("No coaches yet", substring = true).assertIsDisplayed()

        compose.onNode(hasSetTextAction()).performTextInput("pro@example.test")
        compose.onNodeWithText("Reflections and notes").performScrollTo().performClick()
        compose.onNodeWithText("Send invitation").performScrollTo().performClick()

        compose.onNodeWithText("Invitation sent to pro@example.test.").assertExists()
        assertEquals(listOf(Triple("pro@example.test", false, true)), coaching.invited)

        compose.onNode(hasContentDescription("Cancel the invitation to pro@example.test")).performScrollTo().performClick()
        compose.onNodeWithText("No coaches yet", substring = true).assertExists()
    }

    @Test
    fun `shows an active coach and what they see`() {
        show(FakeCoachingRepository(coaches = listOf(Fixtures.coach("Mark"))))

        compose.onNodeWithText("Mark").assertIsDisplayed()
        compose.onNodeWithText("Sees: Rounds and progress, Practice").assertIsDisplayed()
    }

    @Test
    fun `offers Try again when the list can't load`() {
        show(FakeCoachingRepository(listFailsWith = IOException("offline")))

        compose.onNodeWithText("Couldn't load your coaches.").assertIsDisplayed()
        compose.onNodeWithText("Try again").assertIsDisplayed()
    }

    @Test
    fun `offline, an invitation can't be sent, and it says why`() {
        show(FakeCoachingRepository(), online = false)

        compose.onNodeWithText("You're offline. Connect to send an invitation.").assertExists()
        compose.onNodeWithText("Send invitation").assertIsNotEnabled()
    }
}
