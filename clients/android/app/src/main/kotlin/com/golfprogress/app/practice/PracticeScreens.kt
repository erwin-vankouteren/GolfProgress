package com.golfprogress.app.practice

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.SessionType
import com.golfprogress.api.generated.models.CreatePracticeSessionRequest
import com.golfprogress.api.generated.models.PracticeSessionResponse
import com.golfprogress.api.knownSessionType
import com.golfprogress.app.R
import com.golfprogress.app.rounds.DateField
import com.golfprogress.app.rounds.Fact
import com.golfprogress.app.rounds.FormProblem
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.EmptyState
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import java.util.UUID

/** The session type's name; a type this build doesn't know (#134) reads as "Practice". */
@Composable
fun sessionTypeName(type: SessionType): String =
    stringResource(
        when (type) {
            SessionType.Range -> R.string.session_range
            SessionType.Putting -> R.string.session_putting
            SessionType.ShortGame -> R.string.session_short_game
            SessionType.Simulator -> R.string.session_simulator
            SessionType.Lesson -> R.string.session_lesson
            SessionType.Other -> R.string.session_other
            SessionType.Unknown -> R.string.session_unknown
        },
    )

// ---------------------------------------------------------------- list

/** The Practice tab (#105): every session, newest first, with Log practice. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PracticeListScreen(
    onOpen: (UUID) -> Unit,
    onLogPractice: () -> Unit,
    viewModel: PracticeListViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    val list = rememberLazyListState()
    LaunchedEffect(list, state.sessions.size) {
        snapshotFlow {
            list.layoutInfo.visibleItemsInfo
                .lastOrNull()
                ?.index ?: 0
        }.collect { last -> if (last >= state.sessions.size - 3) viewModel.loadMore() }
    }

    PullToRefreshBox(isRefreshing = state.refreshing, onRefresh = viewModel::refresh, modifier = Modifier.fillMaxSize()) {
        LazyColumn(
            state = list,
            contentPadding = PaddingValues(DesignTokens.Spacing.lg),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm),
            modifier = Modifier.fillMaxSize(),
        ) {
            when (state.first) {
                Load.Loading -> item { Skeleton(blocks = 4, blockHeight = 64) }
                is Load.Failed -> item { LoadError(R.string.practice_load_error, viewModel::retry) }
                is Load.Loaded ->
                    if (state.sessions.isEmpty()) {
                        item { EmptyState(R.string.practice_empty, action = R.string.log_practice, onAction = onLogPractice) }
                    } else {
                        item {
                            Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md)) {
                                Button(onClick = onLogPractice, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                                    Text(stringResource(R.string.log_practice))
                                }
                                Text(
                                    stringResource(R.string.practice_habits_hint),
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                        items(state.sessions, key = { it.practiceSessionId }) { session ->
                            SessionRow(session) { onOpen(session.practiceSessionId) }
                        }
                        if (state.hasMore) {
                            item {
                                Box(Modifier.fillMaxWidth().padding(DesignTokens.Spacing.md), contentAlignment = Alignment.Center) {
                                    if (state.loadingMore) {
                                        CircularProgressIndicator()
                                    } else {
                                        OutlinedButton(
                                            onClick = viewModel::loadMore,
                                            modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                                        ) {
                                            Text(stringResource(R.string.show_more_practice))
                                        }
                                    }
                                }
                            }
                        }
                    }
            }
        }
    }
}

@Composable
private fun SessionRow(
    session: PracticeSessionResponse,
    onOpen: () -> Unit,
) {
    val date = formatDate(session.practicedAt)
    val type = sessionTypeName(session.knownSessionType)
    val duration = session.durationMinutes?.let { stringResource(R.string.duration_minutes, it) }
    Surface(
        shape = RoundedCornerShape(DesignTokens.Radius.md),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                .clickable(role = Role.Button, onClick = onOpen)
                .clearAndSetSemantics { contentDescription = listOfNotNull(date, type, duration).joinToString(", ") },
    ) {
        Row(Modifier.padding(DesignTokens.Spacing.lg), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(date, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                Text(type, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            duration?.let { Text(it, style = MaterialTheme.typography.bodyMedium) }
        }
    }
}

// ---------------------------------------------------------------- log

/** Log practice (#105); [onSaved] opens the new session. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun LogPracticeScreen(
    onSaved: (UUID) -> Unit,
    viewModel: LogPracticeViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.savedId) { state.savedId?.let(onSaved) }
    val focus = remember { PracticeField.entries.associateWith { FocusRequester() } }
    LaunchedEffect(state.focus) {
        state.focus?.let {
            focus.getValue(it).requestFocus()
            viewModel.focusHandled()
        }
    }
    val values = state.values

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        DateField(values.practicedAt, viewModel::setDate)

        Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
            Text(stringResource(R.string.session_type), style = MaterialTheme.typography.labelLarge)
            FlowRow(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                CreatePracticeSessionRequest.SessionType.entries.forEach { type ->
                    FilterChip(
                        selected = values.sessionType == type,
                        onClick = { viewModel.setType(type) },
                        label = { Text(sessionTypeName(SessionType.of(type.value))) },
                        modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                    )
                }
            }
        }

        Field(
            label = R.string.duration,
            value = values.durationMinutes,
            onChange = viewModel::setDuration,
            message = state.messageFor(PracticeField.DurationMinutes),
            focus = focus.getValue(PracticeField.DurationMinutes),
            keyboard = KeyboardType.Number,
            suffix = stringResource(R.string.minutes_unit),
        )
        Field(
            R.string.venue,
            values.venueName,
            viewModel::setVenue,
            state.messageFor(PracticeField.VenueName),
            focus.getValue(PracticeField.VenueName),
        )
        Field(
            R.string.goal,
            values.primaryGoal,
            viewModel::setGoal,
            state.messageFor(PracticeField.PrimaryGoal),
            focus.getValue(PracticeField.PrimaryGoal),
        )

        val tagProblem =
            when (state.tagProblem) {
                TagProblem.Duplicate -> stringResource(R.string.tag_duplicate)
                TagProblem.TooLong -> stringResource(R.string.tag_too_long, MAX_TAG_LENGTH)
                TagProblem.TooMany -> stringResource(R.string.too_many_tags, MAX_TAGS)
                null -> state.messageFor(PracticeField.Tags)?.text()
            }
        OutlinedTextField(
            value = state.tagInput,
            onValueChange = viewModel::setTagInput,
            label = { Text(stringResource(R.string.tags)) },
            isError = tagProblem != null,
            supportingText = { Text(tagProblem ?: stringResource(R.string.tags_hint)) },
            singleLine = true,
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
            keyboardActions = KeyboardActions(onDone = { viewModel.commitTag() }),
            modifier = Modifier.fillMaxWidth().focusRequester(focus.getValue(PracticeField.Tags)),
        )
        if (values.tags.isNotEmpty()) {
            FlowRow(horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
                values.tags.forEach { tag ->
                    val remove = stringResource(R.string.remove_tag, tag)
                    InputChip(
                        selected = false,
                        onClick = { viewModel.removeTag(tag) },
                        label = { Text(tag) },
                        trailingIcon = { Icon(Icons.Filled.Close, contentDescription = null) },
                        modifier = Modifier.heightIn(min = DesignTokens.touchTarget).semantics { contentDescription = remove },
                    )
                }
            }
        }

        OutlinedTextField(
            value = values.notes,
            onValueChange = viewModel::setNotes,
            label = { Text(stringResource(R.string.notes)) },
            isError = state.messageFor(PracticeField.Notes) != null,
            supportingText = state.messageFor(PracticeField.Notes)?.let { { Text(it.text()) } },
            minLines = 3,
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences),
            modifier = Modifier.fillMaxWidth().focusRequester(focus.getValue(PracticeField.Notes)),
        )

        state.problem?.let {
            Text(
                stringResource(if (it == FormProblem.SaveFailed) R.string.practice_save_failed else R.string.auth_cant_connect),
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        if (!online) {
            Text(
                stringResource(R.string.offline_cant_save_practice),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Button(
            onClick = viewModel::save,
            enabled = online && !state.saving,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(if (state.saving) R.string.saving else R.string.save)) }
    }
}

@Composable
private fun Field(
    label: Int,
    value: String,
    onChange: (String) -> Unit,
    message: PracticeMessage?,
    focus: FocusRequester,
    keyboard: KeyboardType = KeyboardType.Text,
    suffix: String? = null,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(stringResource(label)) },
        isError = message != null,
        supportingText = message?.let { { Text(it.text()) } },
        suffix = suffix?.let { { Text(it) } },
        singleLine = true,
        keyboardOptions =
            KeyboardOptions(
                keyboardType = keyboard,
                capitalization = if (keyboard == KeyboardType.Text) KeyboardCapitalization.Sentences else KeyboardCapitalization.None,
                imeAction = ImeAction.Next,
            ),
        modifier = Modifier.fillMaxWidth().focusRequester(focus),
    )
}

@Composable
private fun PracticeMessage.text(): String =
    when (this) {
        is PracticeMessage.FromApi -> text
        is PracticeMessage.Rule ->
            when (val e = error) {
                PracticeError.WholeNumber -> stringResource(R.string.whole_number)
                is PracticeError.DurationRange -> stringResource(R.string.duration_range, e.max)
                is PracticeError.TooLong -> stringResource(R.string.at_most_characters, e.max)
                is PracticeError.TooManyTags -> stringResource(R.string.too_many_tags, e.max)
            }
    }

// ---------------------------------------------------------------- detail

/** A practice session (#105): every field, and Delete behind a confirmation. */
@Composable
fun PracticeDetailScreen(
    onDeleted: () -> Unit,
    viewModel: PracticeDetailViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.deleted) { if (state.deleted) onDeleted() }

    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        when (val loaded = state.session) {
            Load.Loading -> Skeleton(blocks = 2)
            is Load.Failed -> LoadError(R.string.practice_session_load_error, viewModel::retry)
            is Load.Loaded -> {
                val session = loaded.value
                if (session == null) {
                    Text(stringResource(R.string.practice_no_longer_exists), style = MaterialTheme.typography.bodyLarge)
                } else {
                    SessionDetail(session)
                    OutlinedButton(
                        onClick = viewModel::askToDelete,
                        enabled = online,
                        modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
                    ) { Text(stringResource(R.string.delete_practice), color = MaterialTheme.colorScheme.error) }
                }
            }
        }
    }

    if (state.confirmDelete) {
        ConfirmDialog(
            title = R.string.delete_practice_title,
            body = R.string.delete_practice_body,
            confirm = R.string.delete_practice,
            busy = state.deleting,
            error = if (state.deleteFailed) stringResource(R.string.delete_practice_failed) else null,
            onConfirm = viewModel::confirmDelete,
            onDismiss = viewModel::cancelDelete,
        )
    }
}

@Composable
private fun SessionDetail(session: PracticeSessionResponse) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md)) {
        Text(
            formatDate(session.practicedAt),
            style = MaterialTheme.typography.headlineMedium,
            modifier = Modifier.semantics { heading() },
        )
        Fact(stringResource(R.string.session_type), sessionTypeName(session.knownSessionType))
        session.durationMinutes?.let { Fact(stringResource(R.string.duration), stringResource(R.string.duration_minutes, it)) }
        session.venueName?.let { Fact(stringResource(R.string.venue), it) }
        session.primaryGoal?.let { Fact(stringResource(R.string.goal), it) }
        if (session.tags.isNotEmpty()) Fact(stringResource(R.string.tags), session.tags.joinToString(", "))
        session.notes?.let { Fact(stringResource(R.string.notes), it) }
        Text(
            stringResource(R.string.practice_habits_hint),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
