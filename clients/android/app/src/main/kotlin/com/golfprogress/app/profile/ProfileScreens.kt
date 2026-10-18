package com.golfprogress.app.profile

import android.content.Intent
import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.intl.LocaleList
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.core.net.toUri
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.BuildConfig
import com.golfprogress.app.R
import com.golfprogress.app.language.AppLanguage
import com.golfprogress.app.language.AppLocales
import com.golfprogress.app.language.shownLanguage
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import com.golfprogress.app.ui.theme.LocalPalette

// ---------------------------------------------------------------- preference controls

private val unitLabels = mapOf(UserResponse.Units.Metric to R.string.units_metric, UserResponse.Units.Imperial to R.string.units_imperial)

private val trackingLabels =
    mapOf(
        UserResponse.TrackingMode.Minimal to (R.string.tracking_minimal to R.string.tracking_minimal_hint),
        UserResponse.TrackingMode.Balanced to (R.string.tracking_balanced to R.string.tracking_balanced_hint),
        UserResponse.TrackingMode.Detailed to (R.string.tracking_detailed to R.string.tracking_detailed_hint),
    )

private val goalLabels =
    mapOf(
        GoalArea.Putting to R.string.goal_putting,
        GoalArea.Penalties to R.string.goal_penalties,
        GoalArea.TeeShots to R.string.goal_tee_shots,
        GoalArea.Consistency to R.string.goal_consistency,
    )

/** A whole row that selects, as a radio button with its label (and hint). */
@Composable
private fun RadioRow(
    selected: Boolean,
    onClick: () -> Unit,
    @StringRes label: Int,
    @StringRes hint: Int? = null,
) = RadioRow(selected, onClick, AnnotatedString(stringResource(label)), hint?.let { AnnotatedString(stringResource(it)) })

@Composable
private fun RadioRow(
    selected: Boolean,
    onClick: () -> Unit,
    label: AnnotatedString,
    hint: AnnotatedString?,
) {
    Row(
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                .selectable(selected = selected, onClick = onClick, role = Role.RadioButton),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        RadioButton(selected = selected, onClick = null)
        Column(Modifier.padding(start = DesignTokens.Spacing.sm)) {
            Text(label, style = MaterialTheme.typography.bodyLarge)
            hint?.let {
                Text(
                    it,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
fun UnitsChoice(
    value: UserResponse.Units,
    onChange: (UserResponse.Units) -> Unit,
) {
    Column(Modifier.selectableGroup()) {
        UserResponse.Units.entries.forEach { RadioRow(value == it, { onChange(it) }, unitLabels.getValue(it)) }
    }
}

@Composable
fun TrackingChoice(
    value: UserResponse.TrackingMode,
    onChange: (UserResponse.TrackingMode) -> Unit,
) {
    Column(Modifier.selectableGroup()) {
        UserResponse.TrackingMode.entries.forEach { mode ->
            val (label, hint) = trackingLabels.getValue(mode)
            RadioRow(value == mode, { onChange(mode) }, label, hint)
        }
        Hint(stringResource(R.string.can_change_any_time))
    }
}

/**
 * Each language by its own name, as language pickers do, so it can be found whatever
 * the app shows; the name carries its locale, so TalkBack says it in that language.
 * The device's language is named next to its option.
 */
@Composable
fun LanguageChoice(
    value: AppLanguage?,
    onChange: (AppLanguage?) -> Unit,
) {
    val context = LocalContext.current
    val device = shownLanguage(null, AppLocales.deviceTags(context))
    Column(Modifier.selectableGroup()) {
        Hint(stringResource(R.string.language_hint))
        RadioRow(value == null, { onChange(null) }, AnnotatedString(stringResource(R.string.language_device)), ownName(device))
        AppLanguage.entries.forEach { language ->
            RadioRow(value == language, { onChange(language) }, ownName(language), null)
        }
    }
}

private fun ownName(language: AppLanguage) =
    buildAnnotatedString {
        withStyle(SpanStyle(localeList = LocaleList(language.tag))) { append(language.ownName) }
    }

/** Up to three; once three are ticked, the others are disabled until one is unticked. */
@Composable
fun GoalsChoice(
    value: List<GoalArea>,
    onToggle: (GoalArea) -> Unit,
) {
    val full = value.size >= MAX_GOAL_FOCUS
    Column {
        Hint(stringResource(R.string.goal_focus_hint, MAX_GOAL_FOCUS))
        GoalArea.entries.forEach { area ->
            val checked = area in value
            val enabled = checked || !full
            Row(
                modifier =
                    Modifier
                        .fillMaxWidth()
                        .heightIn(min = DesignTokens.touchTarget)
                        .toggleable(value = checked, enabled = enabled, role = Role.Checkbox, onValueChange = { onToggle(area) }),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Checkbox(checked = checked, onCheckedChange = null, enabled = enabled)
                Text(
                    stringResource(goalLabels.getValue(area)),
                    style = MaterialTheme.typography.bodyLarge,
                    color = if (enabled) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = DesignTokens.Spacing.sm),
                )
            }
        }
    }
}

@Composable
private fun Label(
    @StringRes text: Int,
) {
    Text(stringResource(text), style = MaterialTheme.typography.labelLarge)
}

@Composable
private fun Hint(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
}

@Composable
private fun Saved(
    @StringRes text: Int,
) {
    Text(
        stringResource(text),
        color = LocalPalette.current.success,
        style = MaterialTheme.typography.labelLarge,
        modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
    )
}

@Composable
private fun Problem(problem: AccountProblem?) {
    val text =
        when (problem) {
            null -> return
            AccountProblem.Failed -> stringResource(R.string.profile_save_failed)
            AccountProblem.CantConnect -> stringResource(R.string.auth_cant_connect)
            is AccountProblem.TooManyAttempts ->
                problem.seconds?.let { pluralStringResource(R.plurals.auth_too_many_attempts, it, it) }
                    ?: stringResource(R.string.auth_too_many_attempts_later)
        }
    Text(text, color = MaterialTheme.colorScheme.error, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite })
}

// ---------------------------------------------------------------- onboarding

/**
 * Onboarding after Create account (#113): one question per step, then [onFinished] opens
 * Log round. "Skip for now" goes straight to Home.
 */
@Composable
fun OnboardingScreen(
    onFinished: () -> Unit,
    onSkip: () -> Unit,
    viewModel: OnboardingViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.done) { if (state.done) onFinished() }
    val titles = listOf(R.string.onboarding_units, R.string.onboarding_tracking, R.string.onboarding_goals)

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .safeDrawingPadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.xl),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Column(Modifier.widthIn(max = 520.dp).fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg)) {
            Hint(stringResource(R.string.onboarding_step, state.step + 1, OnboardingState.STEPS))
            Text(
                stringResource(titles[state.step]),
                style = MaterialTheme.typography.headlineMedium,
                modifier = Modifier.semantics { heading() },
            )
            val preferences = state.preferences
            when (state.step) {
                0 -> UnitsChoice(preferences.units) { viewModel.setPreferences(preferences.copy(units = it)) }
                1 -> TrackingChoice(preferences.trackingMode) { viewModel.setPreferences(preferences.copy(trackingMode = it)) }
                else -> GoalsChoice(preferences.goalFocus, viewModel::toggleGoal)
            }
            Problem(state.problem)
            if (state.last && !online) Hint(stringResource(R.string.offline_cant_save_profile))
            Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md)) {
                if (state.step > 0) {
                    OutlinedButton(onClick = viewModel::back, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                        Text(stringResource(R.string.navigate_back))
                    }
                }
                Button(
                    onClick = viewModel::next,
                    enabled = !state.last || (online && !state.saving),
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                ) {
                    Text(
                        stringResource(
                            if (!state.last) {
                                R.string.next
                            } else if (state.saving) {
                                R.string.saving
                            } else {
                                R.string.log_first_round
                            },
                        ),
                    )
                }
            }
            TextButton(onClick = onSkip, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                Text(stringResource(R.string.skip_for_now))
            }
        }
    }
}

// ---------------------------------------------------------------- profile

/**
 * Profile (#113): your details, preferences, the account (change password, sign out,
 * delete) and About. After deleting the account, [onSignOut] clears the session.
 */
@Composable
fun ProfileScreen(
    onSignOut: () -> Unit,
    viewModel: ProfileViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.deleted) { if (state.deleted) onSignOut() }

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val me = state.me) {
            Load.Loading -> Skeleton(blocks = 2)
            is Load.Failed -> LoadError(R.string.profile_load_error, viewModel::retry)
            is Load.Loaded -> {
                DetailsCard(state, me.value.email, viewModel, online)
                PreferencesCard(state, viewModel, online)
            }
        }
        // The account and About need nothing from GET /me: signing out always works.
        AccountCard(state, viewModel, onSignOut, online)
        AboutCard()
    }

    if (state.confirmDelete) {
        ConfirmDialog(
            title = R.string.delete_account_title,
            body = R.string.delete_account_body,
            confirm = R.string.delete_my_account,
            busy = state.deleting,
            error =
                state.deleteError
                    ?: if (state.deletePasswordMissing) {
                        stringResource(
                            R.string.auth_required,
                            stringResource(R.string.auth_password),
                        )
                    } else {
                        null
                    },
            onConfirm = viewModel::confirmDelete,
            onDismiss = viewModel::cancelDelete,
        ) {
            val focus = remember { FocusRequester() }
            LaunchedEffect(Unit) { focus.requestFocus() }
            OutlinedTextField(
                value = state.deletePassword,
                onValueChange = viewModel::setDeletePassword,
                label = { Text(stringResource(R.string.auth_password)) },
                singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                modifier = Modifier.fillMaxWidth().focusRequester(focus),
            )
            state.deleteProblem?.let { Problem(it) }
        }
    }
}

@Composable
private fun DetailsCard(
    state: ProfileState,
    email: String?,
    viewModel: ProfileViewModel,
    online: Boolean,
) {
    val values = state.details
    SectionCard(R.string.your_details) {
        TextInput(R.string.auth_name, values.displayName, {
            viewModel.setDetails(values.copy(displayName = it))
        }, detailsError(state, ProfileField.DisplayName))
        OutlinedTextField(
            value = email.orEmpty(),
            onValueChange = {},
            readOnly = true,
            label = { Text(stringResource(R.string.auth_email)) },
            supportingText = { Text(stringResource(R.string.email_hint)) },
            modifier = Modifier.fillMaxWidth(),
        )
        TextInput(
            R.string.handicap_index,
            values.handicapIndex,
            { viewModel.setDetails(values.copy(handicapIndex = it)) },
            detailsError(state, ProfileField.HandicapIndex),
            KeyboardType.Decimal,
            optional = true,
        )
        TextInput(
            R.string.typical_18_score,
            values.typical18Score,
            { viewModel.setDetails(values.copy(typical18Score = it)) },
            detailsError(state, ProfileField.Typical18Score),
            KeyboardType.Number,
            optional = true,
        )
        if (state.detailsSaved) Saved(R.string.changes_saved)
        Problem(state.detailsProblem)
        SaveButton(R.string.save, state.detailsSaving, online, viewModel::saveDetails)
    }
}

@Composable
private fun detailsError(
    state: ProfileState,
    field: ProfileField,
): String? {
    state.detailsApiErrors[field]?.let { return it }
    if (!state.detailsSubmitted) return null
    return when (val e = state.detailsErrors[field]) {
        null -> null
        ProfileError.Required -> stringResource(R.string.auth_required, stringResource(R.string.auth_name))
        is ProfileError.TooLong -> stringResource(R.string.at_most_characters, e.max)
        ProfileError.NotANumber -> stringResource(R.string.enter_a_number)
        ProfileError.NotWhole -> stringResource(R.string.whole_number)
        is ProfileError.OutOfRange -> stringResource(R.string.number_range, e.min.toInt(), e.max.toInt())
    }
}

@Composable
private fun PreferencesCard(
    state: ProfileState,
    viewModel: ProfileViewModel,
    online: Boolean,
) {
    val preferences = state.preferences
    SectionCard(R.string.preferences) {
        Label(R.string.units)
        UnitsChoice(preferences.units) { viewModel.setPreferences(preferences.copy(units = it)) }
        Label(R.string.tracking_mode)
        TrackingChoice(preferences.trackingMode) { viewModel.setPreferences(preferences.copy(trackingMode = it)) }
        Label(R.string.goal_focus)
        GoalsChoice(preferences.goalFocus, viewModel::toggleGoal)
        Label(R.string.language)
        LanguageChoice(preferences.language) { viewModel.setPreferences(preferences.copy(language = it)) }
        if (state.preferencesSaved) Saved(R.string.changes_saved)
        // Once saved, the app switches to it (and does nothing if it's shown already).
        val context = LocalContext.current
        LaunchedEffect(state.preferencesSaved) {
            if (state.preferencesSaved) AppLocales.apply(context, preferences.language)
        }
        Problem(state.preferencesProblem)
        SaveButton(R.string.save, state.preferencesSaving, online, viewModel::savePreferences)
    }
}

@Composable
private fun AccountCard(
    state: ProfileState,
    viewModel: ProfileViewModel,
    onSignOut: () -> Unit,
    online: Boolean,
) {
    SectionCard(R.string.account) {
        Text(
            stringResource(R.string.change_password),
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.semantics { heading() },
        )
        val currentError =
            state.currentPasswordApiError
                ?: if (state.passwordSubmitted && state.currentPassword.isEmpty()) {
                    stringResource(R.string.auth_required, stringResource(R.string.current_password))
                } else {
                    null
                }
        val newError =
            state.newPasswordApiError ?: if (state.passwordSubmitted) state.newPasswordError?.let { passwordErrorText(it) } else null
        PasswordInput(R.string.current_password, state.currentPassword, viewModel::setCurrentPassword, currentError, null)
        PasswordInput(
            R.string.new_password,
            state.newPassword,
            viewModel::setNewPassword,
            newError,
            stringResource(R.string.new_password_hint),
        )
        if (state.passwordChanged) Saved(R.string.password_changed)
        Problem(state.passwordProblem)
        SaveButton(R.string.change_password, state.passwordSaving, online, viewModel::changePassword, outlined = true)

        OutlinedButton(onClick = onSignOut, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.auth_sign_out))
        }
        Hint(stringResource(R.string.sign_out_hint))

        OutlinedButton(onClick = viewModel::askToDelete, enabled = online, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
            Text(stringResource(R.string.delete_account), color = MaterialTheme.colorScheme.error)
        }
        Hint(stringResource(R.string.delete_account_hint))
    }
}

@Composable
private fun passwordErrorText(error: PasswordError): String =
    when (error) {
        PasswordError.Required -> stringResource(R.string.auth_required, stringResource(R.string.new_password))
        is PasswordError.TooShort -> stringResource(R.string.password_too_short, error.min)
        is PasswordError.TooLong -> stringResource(R.string.at_most_characters, error.max)
        PasswordError.OnlySpaces -> stringResource(R.string.password_only_spaces)
        PasswordError.IsEmail -> stringResource(R.string.password_is_email)
        PasswordError.Unchanged -> stringResource(R.string.password_unchanged)
    }

@Composable
private fun AboutCard() {
    val context = LocalContext.current
    SectionCard(R.string.about) {
        Text(stringResource(R.string.app_version, BuildConfig.VERSION_NAME), style = MaterialTheme.typography.bodyLarge)
        // Each link shows once its address is set in the build (none exist before M3).
        listOf(BuildConfig.PRIVACY_POLICY_URL to R.string.privacy_policy, BuildConfig.TERMS_URL to R.string.terms_of_use)
            .filter { it.first.isNotEmpty() }
            .forEach { (url, label) ->
                TextButton(
                    onClick = { context.startActivity(Intent(Intent.ACTION_VIEW, url.toUri())) },
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                ) { Text(stringResource(label)) }
            }
    }
}

@Composable
private fun TextInput(
    @StringRes label: Int,
    value: String,
    onChange: (String) -> Unit,
    error: String?,
    keyboard: KeyboardType = KeyboardType.Text,
    optional: Boolean = false,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(stringResource(label)) },
        isError = error != null,
        supportingText = (error ?: if (optional) stringResource(R.string.optional) else null)?.let { { Text(it) } },
        singleLine = true,
        keyboardOptions =
            KeyboardOptions(
                keyboardType = keyboard,
                capitalization = if (keyboard == KeyboardType.Text) KeyboardCapitalization.Words else KeyboardCapitalization.None,
            ),
        modifier = Modifier.fillMaxWidth(),
    )
}

@Composable
private fun PasswordInput(
    @StringRes label: Int,
    value: String,
    onChange: (String) -> Unit,
    error: String?,
    hint: String?,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(stringResource(label)) },
        isError = error != null,
        supportingText = (error ?: hint)?.let { { Text(it) } },
        singleLine = true,
        visualTransformation = PasswordVisualTransformation(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
        modifier = Modifier.fillMaxWidth(),
    )
}

@Composable
private fun SaveButton(
    @StringRes label: Int,
    saving: Boolean,
    online: Boolean,
    onClick: () -> Unit,
    outlined: Boolean = false,
) {
    if (!online) Hint(stringResource(R.string.offline_cant_save_profile))
    val text = @Composable { Text(stringResource(if (saving) R.string.saving else label)) }
    val modifier = Modifier.heightIn(min = DesignTokens.touchTarget)
    if (outlined) {
        OutlinedButton(onClick = onClick, enabled = online && !saving, modifier = modifier) { text() }
    } else {
        Button(onClick = onClick, enabled = online && !saving, modifier = modifier) { text() }
    }
}
