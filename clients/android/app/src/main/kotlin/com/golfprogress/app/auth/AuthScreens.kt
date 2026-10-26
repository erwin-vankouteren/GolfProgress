package com.golfprogress.app.auth

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.autofill.ContentType
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.contentType
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.golfprogress.app.R
import com.golfprogress.app.session.AuthViewModel
import com.golfprogress.app.session.ServerWakingHint
import com.golfprogress.app.ui.theme.DesignTokens

/** Welcome, Create account and Sign in (docs/clients/wireframes.md → "Authentication", #71). */
@Composable
fun AuthFlow(viewModel: AuthViewModel = hiltViewModel()) {
    AuthFlow(
        signInForm = viewModel.signInForm,
        createAccountForm = viewModel.createAccountForm,
        onSignIn = viewModel::signIn,
        onCreateAccount = viewModel::createAccount,
        forgotForm = viewModel.forgotForm,
        resetRequestedFor = viewModel.resetRequestedFor,
        onRequestReset = viewModel::requestPasswordReset,
    )
}

/** The flow without its view model, for tests. */
@Composable
fun AuthFlow(
    signInForm: AuthForm,
    createAccountForm: AuthForm,
    onSignIn: () -> Unit,
    onCreateAccount: () -> Unit,
    forgotForm: AuthForm = remember { AuthForm(listOf(AuthField.Email), R.string.auth_something_went_wrong) },
    resetRequestedFor: String? = null,
    onRequestReset: () -> Unit = {},
) {
    val navController = rememberNavController()
    // Switching between Sign in and Create account replaces the screen, so Back returns to Welcome.
    val switchTo = { route: String ->
        navController.navigate(route) { popUpTo(WELCOME) }
    }

    NavHost(navController, startDestination = WELCOME) {
        composable(WELCOME) {
            WelcomeScreen(
                onCreateAccount = { navController.navigate(CREATE_ACCOUNT) },
                onSignIn = { navController.navigate(SIGN_IN) },
            )
        }
        composable(SIGN_IN) {
            FormScreen(
                title = R.string.auth_sign_in,
                form = signInForm,
                passwordType = ContentType.Password,
                onSubmit = onSignIn,
                switchLabel = R.string.auth_need_account,
                onSwitch = { switchTo(CREATE_ACCOUNT) },
                forgotPassword = { navController.navigate(FORGOT_PASSWORD) },
            )
        }
        composable(FORGOT_PASSWORD) {
            ForgotPasswordScreen(forgotForm, resetRequestedFor, onRequestReset, onBack = { navController.popBackStack() })
        }
        composable(CREATE_ACCOUNT) {
            FormScreen(
                title = R.string.auth_create_account,
                form = createAccountForm,
                passwordType = ContentType.NewPassword,
                onSubmit = onCreateAccount,
                switchLabel = R.string.auth_have_account,
                onSwitch = { switchTo(SIGN_IN) },
            )
        }
    }
}

private const val WELCOME = "welcome"
private const val SIGN_IN = "sign-in"
private const val CREATE_ACCOUNT = "create-account"
private const val FORGOT_PASSWORD = "forgot-password"

@Composable
private fun WelcomeScreen(
    onCreateAccount: () -> Unit,
    onSignIn: () -> Unit,
) {
    AuthScaffold {
        Text(
            text = stringResource(R.string.app_name),
            style = MaterialTheme.typography.displaySmall,
            modifier = Modifier.semantics { heading() },
        )
        Text(
            text = stringResource(R.string.welcome_body),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Button(onClick = onCreateAccount, modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.auth_create_account))
        }
        OutlinedButton(onClick = onSignIn, modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.auth_sign_in))
        }
    }
}

@Composable
private fun FormScreen(
    @StringRes title: Int,
    form: AuthForm,
    passwordType: ContentType,
    onSubmit: () -> Unit,
    @StringRes switchLabel: Int,
    onSwitch: () -> Unit,
    forgotPassword: (() -> Unit)? = null,
) {
    AuthScaffold {
        Text(
            text = stringResource(title),
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.semantics { heading() },
        )
        form.fields.forEachIndexed { index, field ->
            AuthTextField(
                form = form,
                field = field,
                contentType =
                    when (field) {
                        AuthField.DisplayName -> ContentType.PersonFullName
                        AuthField.Email -> ContentType.Username
                        AuthField.Password -> passwordType
                    },
                isLast = index == form.fields.lastIndex,
                onDone = onSubmit,
            )
        }
        form.formError?.let { error ->
            Text(
                text = error.text(),
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodyMedium,
                // Announced by TalkBack when it appears.
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        val busy = stringResource(R.string.auth_working)
        Button(
            onClick = onSubmit,
            enabled = !form.isSubmitting,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) {
            if (form.isSubmitting) {
                CircularProgressIndicator(
                    modifier = Modifier.padding(2.dp).semantics { contentDescription = busy },
                    strokeWidth = 2.dp,
                )
            } else {
                Text(stringResource(title))
            }
        }
        ServerWakingHint(waiting = form.isSubmitting, modifier = Modifier.fillMaxWidth())
        if (forgotPassword != null) {
            TextButton(onClick = forgotPassword, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                Text(stringResource(R.string.auth_forgot_password))
            }
        }
        TextButton(onClick = onSwitch, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(switchLabel))
        }
    }
}

/**
 * Forgot password (#86): the email address, then the same message whatever the address.
 * The link in the email opens the web app to choose the new password, until the apps
 * open it themselves (app links, M4).
 */
@Composable
private fun ForgotPasswordScreen(
    form: AuthForm,
    sentTo: String?,
    onSubmit: () -> Unit,
    onBack: () -> Unit,
) {
    AuthScaffold {
        Text(
            text = stringResource(R.string.reset_title),
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.semantics { heading() },
        )
        if (sentTo != null) {
            Text(
                text = stringResource(R.string.reset_link_sent, sentTo),
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        } else {
            Text(
                text = stringResource(R.string.reset_intro),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            AuthTextField(form, AuthField.Email, ContentType.Username, isLast = true, onDone = onSubmit)
            form.formError?.let { error ->
                Text(
                    text = error.text(),
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
                )
            }
            Button(
                onClick = onSubmit,
                enabled = !form.isSubmitting,
                modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
            ) { Text(stringResource(if (form.isSubmitting) R.string.auth_working else R.string.reset_send)) }
        }
        TextButton(onClick = onBack, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.reset_back_to_sign_in))
        }
    }
}

/** A field with its error underneath, tagged for Autofill (`contentType`). */
@Composable
private fun AuthTextField(
    form: AuthForm,
    field: AuthField,
    contentType: ContentType,
    isLast: Boolean,
    onDone: () -> Unit,
) {
    val error = form.fieldErrors[field]
    OutlinedTextField(
        value = form.value(field),
        onValueChange = { form.update(field, it) },
        label = { Text(stringResource(field.label)) },
        isError = error != null,
        supportingText = error?.let { { Text(it.text()) } },
        singleLine = true,
        visualTransformation =
            if (field == AuthField.Password) PasswordVisualTransformation() else VisualTransformation.None,
        keyboardOptions =
            KeyboardOptions(
                keyboardType =
                    when (field) {
                        AuthField.Email -> KeyboardType.Email
                        AuthField.Password -> KeyboardType.Password
                        AuthField.DisplayName -> KeyboardType.Text
                    },
                capitalization = if (field == AuthField.DisplayName) KeyboardCapitalization.Words else KeyboardCapitalization.None,
                autoCorrectEnabled = false,
                imeAction = if (isLast) ImeAction.Done else ImeAction.Next,
            ),
        keyboardActions = KeyboardActions(onDone = { onDone() }),
        modifier = Modifier.fillMaxWidth().semantics { this.contentType = contentType },
    )
}

@Composable
private fun AuthScaffold(content: @Composable () -> Unit) {
    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .safeDrawingPadding()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.xl),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Column(
            modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
        ) {
            content()
        }
    }
}

@Composable
fun FormMessage.text(): String =
    when (this) {
        is FormMessage.Required -> stringResource(R.string.auth_required, stringResource(field.label))
        FormMessage.InvalidEmail -> stringResource(R.string.auth_invalid_email)
        is FormMessage.Rejected -> stringResource(message)
        is FormMessage.TooManyAttempts ->
            if (seconds != null) {
                pluralStringResource(R.plurals.auth_too_many_attempts, seconds, seconds)
            } else {
                stringResource(R.string.auth_too_many_attempts_later)
            }
        FormMessage.CantConnect -> stringResource(R.string.auth_cant_connect)
        FormMessage.SomethingWentWrong -> stringResource(R.string.auth_something_went_wrong)
        is FormMessage.FromApi -> text
    }
