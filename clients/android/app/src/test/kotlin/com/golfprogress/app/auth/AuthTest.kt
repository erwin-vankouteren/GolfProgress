package com.golfprogress.app.auth

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.R
import com.golfprogress.app.ui.theme.GolfProgressTheme
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException

/** The sign-in form's checks and error handling (#71). */
class AuthFormTest {
    private fun filledForm() =
        AuthForm(listOf(AuthField.Email, AuthField.Password), R.string.auth_wrong_credentials).apply {
            update(AuthField.Email, "erwin@example.test")
            update(AuthField.Password, "correct horse")
        }

    @Test
    fun `checks required fields and email before calling the API`() =
        runBlocking {
            val form = AuthForm(listOf(AuthField.Email, AuthField.Password), R.string.auth_wrong_credentials)
            var called = false

            form.submit { _, _, _ -> called = true }
            assertEquals(FormMessage.Required(AuthField.Email), form.fieldErrors[AuthField.Email])
            assertEquals(FormMessage.Required(AuthField.Password), form.fieldErrors[AuthField.Password])

            form.update(AuthField.Email, "not-an-email")
            form.submit { _, _, _ -> called = true }
            assertEquals(FormMessage.InvalidEmail, form.fieldErrors[AuthField.Email])
            assertFalse(called)
        }

    @Test
    fun `sends trimmed values but the password as typed`() =
        runBlocking {
            val form = AuthForm(AuthField.entries, R.string.auth_email_taken)
            form.update(AuthField.DisplayName, " Erwin ")
            form.update(AuthField.Email, " erwin@example.test\n")
            form.update(AuthField.Password, " secret ")
            var sent = emptyList<String>()

            val succeeded = form.submit { email, displayName, password -> sent = listOf(email, displayName, password) }

            assertTrue(succeeded)
            assertEquals(listOf("erwin@example.test", "Erwin", " secret "), sent)
        }

    @Test
    fun `shows the rejected message for 401 and 409`() =
        runBlocking {
            for (status in listOf(401, 409)) {
                val form = filledForm()
                form.submit { _, _, _ -> throw ApiProblem(status) }
                assertEquals(FormMessage.Rejected(R.string.auth_wrong_credentials), form.formError)
            }
        }

    @Test
    fun `shows the wait after too many attempts`() =
        runBlocking {
            val form = filledForm()

            form.submit { _, _, _ -> throw ApiProblem(429, retryAfterSeconds = 42) }

            assertEquals(FormMessage.TooManyAttempts(42), form.formError)
        }

    @Test
    fun `puts validation messages next to their fields`() =
        runBlocking {
            val form = filledForm()

            form.submit { _, _, _ ->
                throw ApiProblem(400, errors = mapOf("Password" to listOf("Password must be at least 12 characters.")))
            }

            assertEquals(FormMessage.FromApi("Password must be at least 12 characters."), form.fieldErrors[AuthField.Password])
            assertNull(form.formError)
        }

    @Test
    fun `reports a connection problem`() =
        runBlocking {
            val form = filledForm()

            form.submit { _, _, _ -> throw IOException("offline") }

            assertEquals(FormMessage.CantConnect, form.formError)
        }
}

/** The screens, on the JVM with Robolectric. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class AuthFlowTest {
    @get:Rule
    val compose = createComposeRule()

    private val signIn = AuthForm(listOf(AuthField.Email, AuthField.Password), R.string.auth_wrong_credentials)
    private val createAccount = AuthForm(AuthField.entries, R.string.auth_email_taken)

    private fun show(onSignIn: suspend () -> Unit = {}) {
        compose.setContent {
            GolfProgressTheme {
                AuthFlow(
                    signInForm = signIn,
                    createAccountForm = createAccount,
                    onSignIn = { runBlocking { signIn.submit { _, _, _ -> onSignIn() } } },
                    onCreateAccount = {},
                )
            }
        }
    }

    @Test
    fun `welcome offers both ways in`() {
        show()

        compose.onNode(hasText("GolfProgress") and isHeading()).assertIsDisplayed()
        compose.onNodeWithText("Create account").assertIsDisplayed()
        compose.onNodeWithText("Sign in").assertIsDisplayed()
    }

    @Test
    fun `sign in shows what is missing, then the API's answer`() {
        show(onSignIn = { throw ApiProblem(429, retryAfterSeconds = 42) })

        compose.onNodeWithText("Sign in").performClick()
        compose.onNode(hasText("Sign in") and isHeading()).assertIsDisplayed()

        compose.onNode(hasText("Sign in") and !isHeading()).performClick()
        compose.onNodeWithText("Email is required.").assertIsDisplayed()
        compose.onNodeWithText("Password is required.").assertIsDisplayed()

        compose.onNodeWithText("Email").performTextInput("erwin@example.test")
        compose.onNodeWithText("Password").performTextInput("correct horse")
        compose.onNode(hasText("Sign in") and !isHeading()).performClick()

        compose.onNodeWithText("Too many attempts. Try again in 42 seconds.").assertIsDisplayed()
    }

    @Test
    fun `create account and sign in link to each other`() {
        show()

        compose.onNodeWithText("Create account").performClick()
        compose.onNodeWithText("Already have an account? Sign in").performClick()

        compose.onNode(hasText("Sign in") and isHeading()).assertIsDisplayed()
    }
}
