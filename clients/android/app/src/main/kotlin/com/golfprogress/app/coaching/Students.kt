package com.golfprogress.app.coaching

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.golfprogress.api.generated.models.StudentResponse
import com.golfprogress.app.R
import com.golfprogress.app.data.StudentRepository
import com.golfprogress.app.ui.common.EmptyState
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.load
import com.golfprogress.app.ui.theme.DesignTokens
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

data class StudentsState(
    val first: Load<Unit> = Load.Loading,
    val students: List<StudentResponse> = emptyList(),
    val total: Int? = null,
    val loadingMore: Boolean = false,
    val moreFailed: Boolean = false,
    val refreshing: Boolean = false,
) {
    val hasMore: Boolean get() = total?.let { students.size < it } ?: false
}

/**
 * Students (#208, docs/coach-access.md → "Screens"): the golfers you coach, newest activity
 * first, a page at a time. Reloads when it comes back into view, so unread replies and a
 * student who removed you show what's true now.
 */
@HiltViewModel
class StudentsViewModel
    @Inject
    constructor(
        private val repository: StudentRepository,
    ) : ViewModel() {
        private val mutableState = MutableStateFlow(StudentsState())
        val state: StateFlow<StudentsState> = mutableState.asStateFlow()
        private var loading: Job? = null

        init {
            reload()
        }

        fun retry() {
            mutableState.update { it.copy(first = Load.Loading) }
            reload()
        }

        fun refresh() {
            if (state.value.first is Load.Loaded) mutableState.update { it.copy(refreshing = true) }
            reload()
        }

        private fun reload() {
            loading?.cancel()
            loading =
                viewModelScope.launch {
                    val page = load { repository.students(skip = 0, take = PAGE_SIZE) }
                    mutableState.update {
                        when (page) {
                            is Load.Loaded ->
                                it.copy(
                                    first = Load.Loaded(Unit),
                                    students = page.value.items,
                                    total = page.value.total,
                                    moreFailed = false,
                                )
                            is Load.Failed -> if (it.first is Load.Loaded) it else it.copy(first = page)
                            Load.Loading -> it
                        }.copy(refreshing = false, loadingMore = false)
                    }
                }
        }

        fun loadMore() {
            val current = state.value
            if (!current.hasMore || current.loadingMore || current.refreshing || current.first !is Load.Loaded) return
            mutableState.update { it.copy(loadingMore = true, moreFailed = false) }
            loading =
                viewModelScope.launch {
                    when (val page = load { repository.students(skip = current.students.size, take = PAGE_SIZE) }) {
                        is Load.Loaded ->
                            mutableState.update {
                                val total = if (page.value.items.isEmpty()) it.students.size else page.value.total ?: it.total
                                it.copy(students = it.students + page.value.items, total = total, loadingMore = false)
                            }
                        else -> mutableState.update { it.copy(loadingMore = false, moreFailed = true) }
                    }
                }
        }

        companion object {
            const val PAGE_SIZE = 20
        }
    }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudentsScreen(
    onOpenStudent: (UUID) -> Unit,
    viewModel: StudentsViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.refresh() }
    PullToRefreshBox(isRefreshing = state.refreshing, onRefresh = viewModel::refresh, modifier = Modifier.fillMaxSize()) {
        StudentsContent(state, onOpenStudent, viewModel::retry, viewModel::loadMore)
    }
}

@Composable
fun StudentsContent(
    state: StudentsState,
    onOpenStudent: (UUID) -> Unit,
    onRetry: () -> Unit = {},
    onLoadMore: () -> Unit = {},
) {
    LazyColumn(
        contentPadding = PaddingValues(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm),
        modifier = Modifier.fillMaxSize(),
    ) {
        when (state.first) {
            Load.Loading -> item { Skeleton(blocks = 3, blockHeight = 72) }
            is Load.Failed -> item { LoadError(R.string.students_load_error, onRetry) }
            is Load.Loaded ->
                if (state.students.isEmpty()) {
                    item { EmptyState(R.string.students_empty) }
                } else {
                    items(state.students, key = { it.golferId }) { student ->
                        StudentRow(student) { onOpenStudent(student.golferId) }
                    }
                    if (state.hasMore) {
                        item {
                            Box(Modifier.fillMaxWidth().padding(DesignTokens.Spacing.md), contentAlignment = Alignment.Center) {
                                if (state.loadingMore) {
                                    CircularProgressIndicator()
                                } else {
                                    OutlinedButton(onClick = onLoadMore, modifier = Modifier.heightIn(min = DesignTokens.touchTarget)) {
                                        Text(stringResource(R.string.show_more_students))
                                    }
                                }
                            }
                        }
                    }
                }
        }
    }
}

/** Name, last activity, unread replies and what they share; TalkBack reads it as one item. */
@Composable
private fun StudentRow(
    student: StudentResponse,
    onOpen: () -> Unit,
) {
    Surface(
        shape = RoundedCornerShape(DesignTokens.Radius.md),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        modifier =
            Modifier
                .fillMaxWidth()
                .heightIn(min = DesignTokens.touchTarget)
                // Paused without Coaching (#312): nothing of theirs to open.
                .then(if (student.paused) Modifier else Modifier.clickable(role = Role.Button, onClick = onOpen))
                .semantics(mergeDescendants = true) {},
    ) {
        Column(Modifier.padding(DesignTokens.Spacing.lg), verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
            Text(student.displayName, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
            if (student.paused) {
                Hint(stringResource(R.string.student_paused))
                return@Column
            }
            Hint(
                student.lastActivityOn?.let { stringResource(R.string.student_last_activity, formatDate(it.toString())) }
                    ?: stringResource(R.string.student_no_activity),
            )
            if (student.unreadReplies > 0) {
                Text(
                    pluralStringResource(R.plurals.student_unread_replies, student.unreadReplies, student.unreadReplies),
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
            Hint(studentShares(student.shareReflections, student.sharePractice))
        }
    }
}

/** "You see: Rounds and progress, Reflections and notes, Practice". */
@Composable
fun studentShares(
    shareReflections: Boolean,
    sharePractice: Boolean,
): String {
    val items = sharedItems(shareReflections, sharePractice).map { stringResource(sharedLabels.getValue(it)) }
    return stringResource(R.string.student_shares, items.joinToString(", "))
}

@Composable
private fun Hint(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
}
