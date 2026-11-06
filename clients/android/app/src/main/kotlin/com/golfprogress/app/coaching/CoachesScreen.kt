package com.golfprogress.app.coaching

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.CoachInvitationResponse
import com.golfprogress.api.generated.models.CoachResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import com.golfprogress.app.ui.theme.LocalPalette

private val sharedLabels =
    mapOf(
        SharedItem.Rounds to R.string.share_rounds,
        SharedItem.Reflections to R.string.share_reflections,
        SharedItem.Practice to R.string.share_practice,
    )

/** Profile → Coaches (#186). Accepting an invitation happens on the web: the email's link opens the browser. */
@Composable
fun CoachesScreen(
    viewModel: CoachesViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val coaches = state.coaches) {
            Load.Loading -> Skeleton(blocks = 2)
            is Load.Failed -> LoadError(R.string.coaches_load_error, viewModel::retry)
            is Load.Loaded -> {
                val active = coaches.value.coaches
                val pending = coaches.value.invitations
                if (active.isEmpty() && pending.isEmpty()) Text(stringResource(R.string.coaches_empty))
                if (active.isNotEmpty()) ActiveCoaches(active)
                if (pending.isNotEmpty()) PendingInvitations(pending, state, viewModel, online)
            }
        }
        // Needs nothing from the list: inviting works even when it can't load.
        InviteCard(state, viewModel, online)
    }
}

@Composable
private fun sharedText(
    shareReflections: Boolean,
    sharePractice: Boolean,
): String {
    val items = sharedItems(shareReflections, sharePractice).map { stringResource(sharedLabels.getValue(it)) }
    return stringResource(R.string.coach_sees, items.joinToString(", "))
}

@Composable
private fun Hint(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
}

@Composable
private fun ActiveCoaches(coaches: List<CoachResponse>) {
    SectionCard(R.string.coaches_active) {
        coaches.forEach { coach ->
            Column {
                Text(coach.displayName, style = MaterialTheme.typography.bodyLarge)
                Hint(sharedText(coach.shareReflections, coach.sharePractice))
            }
        }
    }
}

@Composable
private fun PendingInvitations(
    invitations: List<CoachInvitationResponse>,
    state: CoachesState,
    viewModel: CoachesViewModel,
    online: Boolean,
) {
    SectionCard(R.string.coaches_pending) {
        invitations.forEach { invitation ->
            Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
                Text(invitation.email, style = MaterialTheme.typography.bodyLarge)
                Hint(stringResource(R.string.invitation_expires, formatDate(invitation.expiresAt.toLocalDate().toString())))
                Hint(sharedText(invitation.shareReflections, invitation.sharePractice))
                val cancelLabel = stringResource(R.string.cancel_invitation_of, invitation.email)
                OutlinedButton(
                    onClick = { viewModel.cancel(invitation.coachInvitationId) },
                    enabled = online && invitation.coachInvitationId !in state.cancelling,
                    modifier =
                        Modifier
                            .heightIn(min = DesignTokens.touchTarget)
                            .semantics { contentDescription = cancelLabel },
                ) { Text(stringResource(R.string.cancel)) }
            }
        }
        state.cancelProblem?.let {
            ErrorText(
                stringResource(
                    when (it) {
                        CancelProblem.Failed -> R.string.cancel_invitation_failed
                        CancelProblem.CantConnect -> R.string.auth_cant_connect
                    },
                ),
            )
        }
    }
}

@Composable
private fun InviteCard(
    state: CoachesState,
    viewModel: CoachesViewModel,
    online: Boolean,
) {
    SectionCard(R.string.invite_coach) {
        val label = stringResource(R.string.coach_email)
        val error =
            state.emailApiError
                ?: when (state.emailError) {
                    CoachEmailError.Required -> stringResource(R.string.auth_required, label)
                    CoachEmailError.Invalid -> stringResource(R.string.auth_invalid_email)
                    null -> null
                }
        OutlinedTextField(
            value = state.email,
            onValueChange = viewModel::setEmail,
            label = { Text(label) },
            isError = error != null,
            supportingText = error?.let { { Text(it) } },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, capitalization = KeyboardCapitalization.None),
            modifier = Modifier.fillMaxWidth(),
        )
        Text(stringResource(R.string.what_coach_sees), style = MaterialTheme.typography.labelLarge)
        ShareRow(R.string.share_rounds, R.string.share_rounds_hint, checked = true, enabled = false, onChange = {})
        ShareRow(
            R.string.share_reflections,
            R.string.share_reflections_hint,
            state.shareReflections,
            onChange = viewModel::setShareReflections,
        )
        ShareRow(R.string.share_practice, R.string.share_practice_hint, state.sharePractice, onChange = viewModel::setSharePractice)
        Hint(stringResource(R.string.never_shared))

        state.sent?.let { sent ->
            Text(
                stringResource(if (sent.resent) R.string.invitation_resent else R.string.invitation_sent, sent.email),
                color = LocalPalette.current.success,
                style = MaterialTheme.typography.labelLarge,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        state.inviteProblem?.let {
            ErrorText(
                stringResource(
                    when (it) {
                        InviteProblem.Failed -> R.string.invite_failed
                        InviteProblem.CantConnect -> R.string.auth_cant_connect
                        InviteProblem.TooManyToday -> R.string.too_many_invitations
                    },
                ),
            )
        }
        if (!online) Hint(stringResource(R.string.offline_cant_invite))
        Button(
            onClick = viewModel::send,
            enabled = online && !state.sending,
            modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(if (state.sending) R.string.auth_working else R.string.send_invitation)) }
    }
}

@Composable
private fun ErrorText(text: String) {
    Text(text, color = MaterialTheme.colorScheme.error, modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite })
}

/** A whole row that toggles, as a checkbox with its label and what it shares. */
@Composable
private fun ShareRow(
    @StringRes label: Int,
    @StringRes hint: Int,
    checked: Boolean,
    enabled: Boolean = true,
    onChange: (Boolean) -> Unit,
) {
    Row(
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                .toggleable(value = checked, enabled = enabled, role = Role.Checkbox, onValueChange = onChange),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Checkbox(checked = checked, onCheckedChange = null, enabled = enabled)
        Column(Modifier.padding(start = DesignTokens.Spacing.sm)) {
            Text(stringResource(label), style = MaterialTheme.typography.bodyLarge)
            Hint(stringResource(hint))
        }
    }
}
