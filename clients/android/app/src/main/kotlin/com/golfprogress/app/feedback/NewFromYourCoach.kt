package com.golfprogress.app.feedback

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import com.golfprogress.api.generated.models.UnreadFeedbackResponse
import com.golfprogress.api.generated.models.UnreadFeedbackThreadResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.SectionCard
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.theme.DesignTokens

/**
 * "New from your coach" on Home (#222, docs/coach-access.md → "Screens"): the newest
 * threads with something unread, each opening what it's about. Shown only while something
 * is unread; opening a thread marks it read, and Home reloads when it's back in view.
 */
@Composable
fun NewFromYourCoachCard(
    unread: UnreadFeedbackResponse,
    onOpen: (UnreadDestination) -> Unit,
) {
    SectionCard(R.string.new_from_your_coach) {
        unread.threads.forEach { UnreadItem(it, onOpen) }
        val more = unreadNotListed(unread)
        if (more > 0) {
            Text(
                pluralStringResource(R.plurals.more_unread_feedback, more, more),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun UnreadItem(
    thread: UnreadFeedbackThreadResponse,
    onOpen: (UnreadDestination) -> Unit,
) {
    val coach = thread.coachName ?: stringResource(R.string.feedback_former_coach)
    val date = thread.onDate?.let { formatDate(it) }
    val about =
        when {
            thread.targetType == UnreadFeedbackThreadResponse.TargetType.PracticeSession ->
                stringResource(R.string.unread_on_session, coach, date.orEmpty())
            thread.targetType == UnreadFeedbackThreadResponse.TargetType.Reflection && date == null ->
                stringResource(R.string.unread_on_reflection_no_date, coach)
            thread.targetType == UnreadFeedbackThreadResponse.TargetType.Reflection ->
                stringResource(R.string.unread_on_reflection, coach, date.orEmpty())
            else -> stringResource(R.string.unread_on_round, coach, date.orEmpty())
        }
    val destination = destinationOf(thread)
    Column(
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                .then(
                    if (destination != null) Modifier.clickable(role = Role.Button) { onOpen(destination) } else Modifier,
                ).semantics(mergeDescendants = true) {},
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs),
    ) {
        Text(about, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
        Text(
            pluralStringResource(R.plurals.new_messages, thread.unread, thread.unread),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(thread.preview, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
