package com.golfprogress.app.feedback

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.FeedbackMessageResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.currentLocale
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.UUID

/** Where a detail screen shows the feedback on an item; empty where there's no golfer to show it for. */
typealias FeedbackSlot = @Composable (FeedbackTarget, UUID) -> Unit

val NoFeedback: FeedbackSlot = { _, _ -> }

/**
 * "Feedback from your coach" under a round, reflection or practice session (#217). Its own
 * view model per item, so a round's and each of its reflections' threads load on their own.
 */
@Composable
fun FeedbackSection(
    golferId: UUID,
    target: FeedbackTarget,
    targetId: UUID,
    viewModel: FeedbackViewModel = hiltViewModel(key = "feedback-$target-$targetId"),
) {
    LaunchedEffect(golferId, target, targetId) { viewModel.start(golferId, target, targetId) }
    val state by viewModel.state.collectAsStateWithLifecycle()
    FeedbackContent(
        state = state,
        actions =
            FeedbackActions(
                onRetry = viewModel::retry,
                onDraft = viewModel::setDraft,
                onSend = viewModel::send,
                onEdit = viewModel::startEdit,
                onEditText = viewModel::setEditText,
                onSaveEdit = viewModel::saveEdit,
                onCancelEdit = viewModel::cancelEdit,
                onAskDelete = viewModel::askDelete,
                onConfirmDelete = viewModel::confirmDelete,
                onCancelDelete = viewModel::cancelDelete,
            ),
    )
}

/** What the section can do, gathered so the content can be shown without a view model. */
data class FeedbackActions(
    val onRetry: () -> Unit = {},
    val onDraft: (UUID, String) -> Unit = { _, _ -> },
    val onSend: (UUID) -> Unit = {},
    val onEdit: (FeedbackMessageResponse) -> Unit = {},
    val onEditText: (String) -> Unit = {},
    val onSaveEdit: () -> Unit = {},
    val onCancelEdit: () -> Unit = {},
    val onAskDelete: (UUID) -> Unit = {},
    val onConfirmDelete: () -> Unit = {},
    val onCancelDelete: () -> Unit = {},
)

@Composable
fun FeedbackContent(
    state: FeedbackState,
    actions: FeedbackActions,
    online: Boolean = isOnline(),
) {
    when (val view = state.view) {
        // Nothing until it's known there's something: most golfers have no coach.
        FeedbackView.Loading, FeedbackView.Hidden -> Unit
        FeedbackView.Failed ->
            SectionCard(R.string.feedback_from_your_coach) { LoadError(R.string.feedback_load_error, actions.onRetry) }
        is FeedbackView.Threads ->
            SectionCard(R.string.feedback_from_your_coach) {
                view.threads.forEach { thread ->
                    Thread(thread, showName = view.threads.size > 1, state, actions, online)
                }
            }
    }

    if (state.confirmDelete != null) {
        ConfirmDialog(
            title = R.string.feedback_delete_title,
            body = R.string.feedback_delete_body,
            confirm = R.string.feedback_delete,
            busy = state.deleting,
            error = state.deleteProblem?.let { problemText(it) },
            onConfirm = actions.onConfirmDelete,
            onDismiss = actions.onCancelDelete,
        )
    }
}

@Composable
private fun Thread(
    thread: GolferThread,
    showName: Boolean,
    state: FeedbackState,
    actions: FeedbackActions,
    online: Boolean,
) {
    val name = thread.coachName ?: stringResource(R.string.feedback_former_coach)
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
        // With one coach, the card's title says enough.
        if (showName) {
            Text(
                name,
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.semantics { heading() },
            )
        }
        if (thread.messages.isEmpty()) {
            Text(
                stringResource(R.string.feedback_empty_golfer, name),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        thread.messages.forEach { Message(it, state, actions, online) }
        val coachId = thread.coachId
        if (thread.canWrite && coachId != null) Composer(coachId, name, state, actions, online)
    }
}

@Composable
private fun Message(
    message: FeedbackMessageResponse,
    state: FeedbackState,
    actions: FeedbackActions,
    online: Boolean,
) {
    val locale = currentLocale()
    val author =
        when {
            message.mine -> stringResource(R.string.feedback_you)
            else -> message.coachName ?: stringResource(R.string.feedback_former_coach)
        }
    val whenText = message.createdAt.format(DateTimeFormatter.ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT).withLocale(locale))
    val editing = state.editing?.takeIf { it.first == message.feedbackMessageId }
    val meta =
        listOfNotNull(author, whenText, message.editedAt?.let { stringResource(R.string.feedback_edited) }).joinToString(" · ")

    Column(
        modifier =
            Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(DesignTokens.Radius.md))
                .padding(DesignTokens.Spacing.md),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
    ) {
        Text(meta, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
        if (editing != null) {
            val error = state.saveProblem?.let { problemText(it) }
            OutlinedTextField(
                value = editing.second,
                onValueChange = actions.onEditText,
                label = { Text(stringResource(R.string.feedback_edit_message)) },
                isError = error != null,
                supportingText = error?.let { { Text(it) } },
                modifier = Modifier.fillMaxWidth(),
            )
            Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                Button(
                    onClick = actions.onSaveEdit,
                    enabled = online && !state.saving,
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                ) { Text(stringResource(if (state.saving) R.string.saving else R.string.save)) }
                TextButton(
                    onClick = actions.onCancelEdit,
                    enabled = !state.saving,
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                ) { Text(stringResource(R.string.cancel)) }
            }
        } else {
            Text(message.text, style = MaterialTheme.typography.bodyLarge)
        }
        if (message.mine && editing == null) {
            val editLabel = stringResource(R.string.feedback_edit_of, whenText)
            val deleteLabel = stringResource(R.string.feedback_delete_of, whenText)
            Row(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                TextButton(
                    onClick = { actions.onEdit(message) },
                    enabled = online,
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget).semantics { contentDescription = editLabel },
                ) { Text(stringResource(R.string.feedback_edit)) }
                TextButton(
                    onClick = { actions.onAskDelete(message.feedbackMessageId) },
                    enabled = online,
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget).semantics { contentDescription = deleteLabel },
                ) { Text(stringResource(R.string.feedback_delete)) }
            }
        }
    }
}

@Composable
private fun Composer(
    coachId: UUID,
    coachName: String,
    state: FeedbackState,
    actions: FeedbackActions,
    online: Boolean,
) {
    val sending = state.sendingTo == coachId
    val error = state.sendProblem?.takeIf { it.first == coachId }?.let { problemText(it.second) }
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
        OutlinedTextField(
            value = state.drafts[coachId].orEmpty(),
            onValueChange = { actions.onDraft(coachId, it) },
            label = { Text(stringResource(R.string.feedback_message_to, coachName)) },
            isError = error != null,
            supportingText = error?.let { { Text(it) } },
            minLines = 2,
            modifier = Modifier.fillMaxWidth(),
        )
        if (!online) {
            Text(
                stringResource(R.string.feedback_offline),
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        OutlinedButton(
            onClick = { actions.onSend(coachId) },
            enabled = online && state.sendingTo == null,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(if (sending) R.string.feedback_sending else R.string.feedback_send)) }
    }
}

@Composable
private fun problemText(problem: FeedbackProblem): String =
    when (problem) {
        FeedbackProblem.Empty -> stringResource(R.string.feedback_write_something)
        FeedbackProblem.TooLong -> stringResource(R.string.feedback_too_long, FEEDBACK_TEXT_MAX)
        FeedbackProblem.TooMany -> stringResource(R.string.feedback_too_many)
        FeedbackProblem.Failed -> stringResource(R.string.feedback_failed)
        FeedbackProblem.CantConnect -> stringResource(R.string.auth_cant_connect)
    }
