package com.golfprogress.app.auth

import androidx.annotation.StringRes
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.golfprogress.api.ApiProblem
import com.golfprogress.app.R
import kotlinx.coroutines.CancellationException

enum class AuthField(
    @StringRes val label: Int,
    /** The API's key for this field in a validation problem. */
    val apiName: String,
) {
    DisplayName(R.string.auth_name, "displayName"),
    Email(R.string.auth_email, "email"),
    Password(R.string.auth_password, "password"),
}

/** A message on a sign-in form; the screen turns it into text (`AuthScreens.kt`). */
sealed interface FormMessage {
    data class Required(
        val field: AuthField,
    ) : FormMessage

    data object InvalidEmail : FormMessage

    /** What a 401 (Sign in) or 409 (Create account) means on this form. */
    data class Rejected(
        @StringRes val message: Int,
    ) : FormMessage

    data class TooManyAttempts(
        val seconds: Int?,
    ) : FormMessage

    data object CantConnect : FormMessage

    data object SomethingWentWrong : FormMessage

    /** A validation message from the API, already in the user's words. */
    data class FromApi(
        val text: String,
    ) : FormMessage
}

private val emailShape = Regex("""[^\s@]+@[^\s@]+\.[^\s@]+""")

/**
 * The state of a sign-in form (#71): checks required fields and email shape locally,
 * then shows the API's per-field messages, and its 401/409/429 as a form error.
 */
class AuthForm(
    val fields: List<AuthField>,
    @StringRes private val rejectedMessage: Int,
) {
    private val values = mutableStateMapOf<AuthField, String>()

    var fieldErrors by mutableStateOf<Map<AuthField, FormMessage>>(emptyMap())
        private set
    var formError by mutableStateOf<FormMessage?>(null)
        private set
    var isSubmitting by mutableStateOf(false)
        private set

    fun value(field: AuthField): String = values[field].orEmpty()

    fun update(
        field: AuthField,
        value: String,
    ) {
        values[field] = value
    }

    /**
     * Validates, then runs [action] with the trimmed values (the password as typed).
     * Returns whether it succeeded.
     */
    suspend fun submit(action: suspend (email: String, displayName: String, password: String) -> Unit): Boolean {
        if (isSubmitting) return false
        formError = null
        fieldErrors = validate()
        if (fieldErrors.isNotEmpty()) return false

        isSubmitting = true
        return try {
            action(trimmed(AuthField.Email), trimmed(AuthField.DisplayName), value(AuthField.Password))
            true
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            describe(e)
            false
        } finally {
            isSubmitting = false
        }
    }

    private fun validate(): Map<AuthField, FormMessage> =
        buildMap {
            for (field in fields) {
                val value = if (field == AuthField.Password) value(field) else trimmed(field)
                when {
                    value.isEmpty() -> put(field, FormMessage.Required(field))
                    field == AuthField.Email && !emailShape.matches(value) -> put(field, FormMessage.InvalidEmail)
                }
            }
        }

    private fun describe(error: Exception) {
        if (error !is ApiProblem) {
            formError = FormMessage.CantConnect
            return
        }
        when (error.status) {
            400 -> {
                fieldErrors =
                    fields
                        .mapNotNull { field -> error.messagesFor(field.apiName).firstOrNull()?.let { field to FormMessage.FromApi(it) } }
                        .toMap()
                if (fieldErrors.isEmpty()) formError = FormMessage.SomethingWentWrong
            }
            401, 409 -> formError = FormMessage.Rejected(rejectedMessage)
            429 -> formError = FormMessage.TooManyAttempts(error.retryAfterSeconds)
            else -> formError = FormMessage.SomethingWentWrong
        }
    }

    private fun trimmed(field: AuthField) = value(field).trim()
}
