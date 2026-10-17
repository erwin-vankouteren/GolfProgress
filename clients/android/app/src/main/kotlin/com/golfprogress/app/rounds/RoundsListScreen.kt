package com.golfprogress.app.rounds

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.SwipeToDismissBox
import androidx.compose.material3.SwipeToDismissBoxValue
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.rememberSwipeToDismissBoxState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.ConfirmDialog
import com.golfprogress.app.ui.common.EmptyState
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.theme.DesignTokens
import java.util.UUID

/** The Rounds tab (#97): every round, newest first, a page at a time. */
@Composable
fun RoundsListScreen(
    onOpenRound: (UUID) -> Unit,
    onLogRound: () -> Unit,
    viewModel: RoundsListViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    RoundsListContent(
        state = state,
        onRetry = viewModel::retry,
        onRefresh = viewModel::refresh,
        onLoadMore = viewModel::loadMore,
        onOpenRound = onOpenRound,
        onLogRound = onLogRound,
        onAskToDelete = viewModel::askToDelete,
        onConfirmDelete = viewModel::confirmDelete,
        onCancelDelete = viewModel::cancelDelete,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RoundsListContent(
    state: RoundsListState,
    onRetry: () -> Unit,
    onRefresh: () -> Unit,
    onLoadMore: () -> Unit,
    onOpenRound: (UUID) -> Unit,
    onLogRound: () -> Unit,
    onAskToDelete: (RoundResponse) -> Unit,
    onConfirmDelete: () -> Unit,
    onCancelDelete: () -> Unit,
) {
    val list = rememberLazyListState()
    // Load the next page a few rows before the end.
    LaunchedEffect(list, state.rounds.size) {
        snapshotFlow {
            list.layoutInfo.visibleItemsInfo
                .lastOrNull()
                ?.index ?: 0
        }.collect { last -> if (last >= state.rounds.size - 3) onLoadMore() }
    }

    PullToRefreshBox(isRefreshing = state.refreshing, onRefresh = onRefresh, modifier = Modifier.fillMaxSize()) {
        LazyColumn(
            state = list,
            contentPadding = PaddingValues(DesignTokens.Spacing.lg),
            verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm),
            modifier = Modifier.fillMaxSize(),
        ) {
            when (state.first) {
                Load.Loading -> item { Skeleton(blocks = 4, blockHeight = 64) }
                is Load.Failed -> item { LoadError(R.string.rounds_load_error, onRetry) }
                is Load.Loaded ->
                    if (state.rounds.isEmpty()) {
                        item { EmptyState(R.string.rounds_empty, action = R.string.log_first_round, onAction = onLogRound) }
                    } else {
                        items(state.rounds, key = { it.roundId }) { round ->
                            SwipeToDelete(onDelete = { onAskToDelete(round) }) {
                                RoundRow(round, onOpen = { onOpenRound(round.roundId) }, onDelete = { onAskToDelete(round) })
                            }
                        }
                        if (state.hasMore) item { MoreRounds(state, onLoadMore) }
                    }
            }
        }
    }

    if (state.confirmDelete != null) {
        ConfirmDialog(
            title = R.string.delete_round_title,
            body = R.string.delete_round_body,
            confirm = R.string.delete_round,
            busy = state.deleting,
            error = if (state.deleteFailed) stringResource(R.string.delete_round_failed) else null,
            onConfirm = onConfirmDelete,
            onDismiss = onCancelDelete,
        )
    }
}

/** Swiping a row to the start asks to delete it; the row itself never goes until confirmed. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SwipeToDelete(
    onDelete: () -> Unit,
    content: @Composable () -> Unit,
) {
    val swipe =
        rememberSwipeToDismissBoxState(confirmValueChange = { value ->
            if (value == SwipeToDismissBoxValue.EndToStart) onDelete()
            false
        })
    SwipeToDismissBox(
        state = swipe,
        enableDismissFromStartToEnd = false,
        backgroundContent = {
            Box(
                Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.error, RoundedCornerShape(DesignTokens.Radius.md))
                    .padding(horizontal = DesignTokens.Spacing.lg),
                contentAlignment = Alignment.CenterEnd,
            ) {
                Icon(Icons.Filled.Delete, contentDescription = null, tint = MaterialTheme.colorScheme.onError)
            }
        },
    ) { content() }
}

/**
 * Date, course (or holes), strokes and score. TalkBack reads it as one item, with Delete
 * as an action, since swiping isn't something a screen reader user can discover.
 */
@Composable
private fun RoundRow(
    round: RoundResponse,
    onOpen: () -> Unit,
    onDelete: () -> Unit,
) {
    val deleteLabel = stringResource(R.string.delete_round)
    val strokes = pluralStringResource(R.plurals.strokes_count, round.totalStrokes, round.totalStrokes)
    val score = round.fitnessScore?.let { stringResource(R.string.fitness_score_value, it) } ?: stringResource(R.string.not_scored)
    val place = round.courseName ?: stringResource(R.string.holes_option, round.holes)
    val date = formatDate(round.playedAt)
    Surface(
        shape = RoundedCornerShape(DesignTokens.Radius.md),
        color = MaterialTheme.colorScheme.surface,
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                .clickable(role = Role.Button, onClick = onOpen)
                .clearAndSetSemantics {
                    contentDescription = listOf(date, place, strokes, score).joinToString(", ")
                    customActions =
                        listOf(
                            CustomAccessibilityAction(deleteLabel) {
                                onDelete()
                                true
                            },
                        )
                },
    ) {
        Row(
            modifier = Modifier.padding(DesignTokens.Spacing.lg),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.md),
        ) {
            Column(Modifier.weight(1f)) {
                Text(date, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.SemiBold)
                Text(place, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(strokes, style = MaterialTheme.typography.bodyMedium)
            Text(
                round.fitnessScore?.toString() ?: "–",
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun MoreRounds(
    state: RoundsListState,
    onLoadMore: () -> Unit,
) {
    Box(Modifier.fillMaxWidth().padding(DesignTokens.Spacing.md), contentAlignment = Alignment.Center) {
        if (state.loadingMore) {
            CircularProgressIndicator()
        } else {
            OutlinedButton(onClick = onLoadMore, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                Text(stringResource(R.string.show_more_rounds))
            }
        }
    }
}
