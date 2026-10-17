package com.golfprogress.app.profile

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isDialog
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.data.Change
import com.golfprogress.app.testing.FakeUserRepository
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

/** The profile rules (#113): the web app's profileForm.ts and the API's. */
class ProfileRulesTest {
    @Test
    fun `a name is needed, at most 100 characters as people count them`() {
        assertEquals(ProfileError.Required, validateProfile(ProfileValues(displayName = " "))[ProfileField.DisplayName])
        assertEquals(ProfileError.TooLong(100), validateProfile(ProfileValues(displayName = "x".repeat(101)))[ProfileField.DisplayName])
        assertNull(validateProfile(ProfileValues(displayName = "🏌".repeat(100)))[ProfileField.DisplayName])
    }

    @Test
    fun `handicap from -10 to 54 with a point or comma, typical score whole from 18 to 200`() {
        assertEquals(18.4, parseDecimal("18,4"))
        assertEquals(
            ProfileError.OutOfRange(-10, 54),
            validateProfile(ProfileValues("Erwin", handicapIndex = "54.1"))[ProfileField.HandicapIndex],
        )
        assertEquals(ProfileError.NotWhole, validateProfile(ProfileValues("Erwin", typical18Score = "92.5"))[ProfileField.Typical18Score])
        assertEquals(emptyMap<ProfileField, ProfileError>(), validateProfile(ProfileValues("Erwin", "-10", "92")))
    }

    @Test
    fun `an emptied handicap is cleared, not left alone`() {
        val changes = ProfileValues(" Erwin ", handicapIndex = "", typical18Score = "92").toChanges()

        assertEquals("Erwin", changes.displayName)
        assertEquals(Change<Double?>(null), changes.handicapIndex)
        assertEquals(Change<Int?>(92), changes.typical18Score)
    }

    @Test
    fun `at most three goal areas, in the API's order`() {
        assertEquals(listOf(GoalArea.Putting, GoalArea.Consistency), toggleGoal(listOf(GoalArea.Consistency), GoalArea.Putting))
        val three = listOf(GoalArea.Putting, GoalArea.Penalties, GoalArea.TeeShots)
        assertEquals(three, toggleGoal(three, GoalArea.Consistency))
        assertEquals(listOf(GoalArea.Penalties), toggleGoal(listOf(GoalArea.Putting, GoalArea.Penalties), GoalArea.Putting))
    }

    @Test
    fun `new passwords follow the API's policy`() {
        assertEquals(PasswordError.TooShort(8), checkNewPassword("short", "old", null))
        assertEquals(PasswordError.OnlySpaces, checkNewPassword("        ", "old", null))
        assertEquals(PasswordError.IsEmail, checkNewPassword(" Me@Example.test ", "old", "me@example.test"))
        assertEquals(PasswordError.Unchanged, checkNewPassword("same password", "same password", null))
        assertNull(checkNewPassword("🏌".repeat(8), "old", null))
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class ProfileViewModelTest {
    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    @Test
    fun `preferences save only what they change`() {
        val user = FakeUserRepository()
        val viewModel = ProfileViewModel(user)

        viewModel.setPreferences(
            viewModel.state.value.preferences
                .copy(trackingMode = UserResponse.TrackingMode.Detailed),
        )
        viewModel.toggleGoal(GoalArea.Penalties)
        viewModel.savePreferences()

        val changes = user.updates.single()
        assertEquals("Detailed", changes.trackingMode)
        assertEquals(listOf("Putting", "Penalties"), changes.goalFocus)
        assertNull(changes.displayName)
        assertNull(changes.handicapIndex)
        assertTrue(viewModel.state.value.preferencesSaved)
    }

    @Test
    fun `a wrong current password shows by its field`() {
        val viewModel = ProfileViewModel(FakeUserRepository())

        viewModel.setCurrentPassword("wrong one")
        viewModel.setNewPassword("a brand new passphrase")
        viewModel.changePassword()

        assertEquals("The current password is incorrect.", viewModel.state.value.currentPasswordApiError)
        assertFalse(viewModel.state.value.passwordChanged)
    }

    @Test
    fun `deleting needs the password, then signs out`() {
        val user = FakeUserRepository()
        val viewModel = ProfileViewModel(user)

        viewModel.askToDelete()
        viewModel.confirmDelete()
        assertTrue(viewModel.state.value.deletePasswordMissing)

        viewModel.setDeletePassword("wrong")
        viewModel.confirmDelete()
        assertEquals("The password is incorrect.", viewModel.state.value.deleteError)

        viewModel.setDeletePassword("correct horse")
        viewModel.confirmDelete()
        assertTrue(user.deleted)
        assertTrue(viewModel.state.value.deleted)
    }

    @Test
    fun `onboarding saves the three answers together`() {
        val user = FakeUserRepository()
        val viewModel = OnboardingViewModel(user)

        viewModel.setPreferences(
            viewModel.state.value.preferences
                .copy(units = UserResponse.Units.Imperial),
        )
        viewModel.next()
        viewModel.setPreferences(
            viewModel.state.value.preferences
                .copy(trackingMode = UserResponse.TrackingMode.Balanced),
        )
        viewModel.next()
        viewModel.toggleGoal(GoalArea.Penalties)
        viewModel.next()

        val changes = user.updates.single()
        assertEquals("Imperial", changes.units)
        assertEquals("Balanced", changes.trackingMode)
        assertEquals(listOf("Penalties"), changes.goalFocus)
        assertTrue(changes.timezone!!.isNotEmpty())
        assertTrue(viewModel.state.value.done)
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ProfileScreenTest {
    @get:Rule
    val compose = createComposeRule()

    @Before
    fun mainDispatcher() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun reset() = Dispatchers.resetMain()

    private var signedOut = false

    private fun show(user: FakeUserRepository = FakeUserRepository()): FakeUserRepository {
        val viewModel = ProfileViewModel(user)
        compose.setContent {
            CompositionLocalProvider(LocalOnline provides true) {
                GolfProgressTheme { ProfileScreen(onSignOut = { signedOut = true }, viewModel = viewModel) }
            }
        }
        return user
    }

    @Test
    fun savesDetails_ClearingAnEmptiedHandicap() {
        val user = show()

        compose.onNode(hasText("Your details") and isHeading()).assertIsDisplayed()
        compose.onNode(hasText("Handicap index") and hasSetTextAction()).performTextClearance()
        compose.onAllNodesWithText("Save")[0].performScrollTo().performClick()

        assertEquals(Change<Double?>(null), user.updates.single().handicapIndex)
        compose.onAllNodesWithText("Changes saved.")[0].assertIsDisplayed()
    }

    @Test
    fun checksTheTypicalScoreBeforeSending() {
        val user = show()

        val typical = compose.onNode(hasText("Typical 18-hole score") and hasSetTextAction())
        typical.performTextClearance()
        typical.performTextInput("300")
        compose.onAllNodesWithText("Save")[0].performScrollTo().performClick()

        compose.onNodeWithText("Enter a number from 18 to 200.").assertIsDisplayed()
        assertTrue(user.updates.isEmpty())
    }

    @Test
    fun allowsThreeGoalAreasAtMost() {
        show(FakeUserRepository(me = { Fixtures.user(goalFocus = listOf("Putting", "Penalties", "TeeShots")) }))

        compose.onNodeWithText("Consistency").performScrollTo()
        compose
            .onNode(
                hasText("Consistency") and
                    androidx.compose.ui.test
                        .hasClickAction(),
            ).assertIsNotEnabled()
    }

    @Test
    fun deletesTheAccountAfterAskingForThePassword() {
        val user = show()

        compose.onNodeWithText("Delete account").performScrollTo().performClick()
        compose.onNode(isDialog()).assertIsDisplayed()
        compose.onNode(hasText("Password") and hasSetTextAction()).performTextInput("correct horse")
        compose.onNodeWithText("Delete my account").performClick()

        compose.waitForIdle()
        assertTrue(user.deleted)
        assertTrue(signedOut)
    }
}
