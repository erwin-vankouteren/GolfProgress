package com.golfprogress.app.rounds

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.DateRange
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedIconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.Load
import com.golfprogress.app.ui.common.LoadError
import com.golfprogress.app.ui.common.Skeleton
import com.golfprogress.app.ui.common.formatDate
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset
import java.util.UUID

/** Log round (#94) or Edit round (#97); after Save, [onSaved] gets the round. */
@Composable
fun RoundFormScreen(
    onSaved: (UUID) -> Unit,
    viewModel: RoundFormViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.savedRoundId) { state.savedRoundId?.let(onSaved) }
    val padding = Modifier.padding(DesignTokens.Spacing.lg)

    when {
        state.gone -> RoundGone(padding)
        state.failed ->
            LoadError(
                if (state.editing is Load.Failed) R.string.round_load_error else R.string.preferences_load_error,
                viewModel::retry,
                padding,
            )
        !state.ready -> Skeleton(blocks = 3, modifier = padding)
        else -> RoundForm(state = state, actions = viewModel.actions(), saveLabel = R.string.save)
    }
}

/** A round deleted on another device: say so rather than show an error. */
@Composable
fun RoundGone(modifier: Modifier = Modifier) {
    Text(
        stringResource(R.string.round_no_longer_exists),
        style = MaterialTheme.typography.bodyLarge,
        modifier = modifier,
    )
}

/** What the form can do, so the same form serves Log round and Edit round. */
class RoundFormActions(
    val setDate: (LocalDate) -> Unit,
    val setHoles: (Int) -> Unit,
    val setCourse: (String) -> Unit,
    val setTee: (String) -> Unit,
    val setStrokes: (String) -> Unit,
    val stepStrokes: (Int) -> Unit,
    val setStat: (StatField, String) -> Unit,
    val stepStat: (StatField, Int) -> Unit,
    val setNotes: (String) -> Unit,
    val save: () -> Unit,
    val focusHandled: () -> Unit,
)

fun RoundFormViewModel.actions() =
    RoundFormActions(
        setDate = ::setDate,
        setHoles = ::setHoles,
        setCourse = ::setCourse,
        setTee = ::setTee,
        setStrokes = ::setStrokes,
        stepStrokes = ::stepStrokes,
        setStat = ::setStat,
        stepStat = ::stepStat,
        setNotes = ::setNotes,
        save = ::save,
        focusHandled = ::focusHandled,
    )

/**
 * The round form: date, holes, course, tee, strokes, the tracking mode's stats, notes.
 * Numbers have a numeric keyboard and − / + steppers for one-handed entry on the course;
 * Next moves between fields. Save is disabled offline and while saving.
 */
@Composable
fun RoundForm(
    state: RoundFormState,
    actions: RoundFormActions,
    @StringRes saveLabel: Int,
    online: Boolean = isOnline(),
    extraContent: @Composable () -> Unit = {},
) {
    val values = state.values
    val focus = remember { RoundField.entries.associateWith { FocusRequester() } }
    LaunchedEffect(state.focus) {
        state.focus?.let {
            focus.getValue(it).requestFocus()
            actions.focusHandled()
        }
    }

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        DateField(values.playedAt, actions.setDate)
        HolesField(values.holes, actions.setHoles)
        TextInput(R.string.course, values.courseName, actions.setCourse, focus.getValue(RoundField.CourseName))
        TextInput(R.string.tee, values.teeName, actions.setTee, focus.getValue(RoundField.TeeName))
        NumberInput(
            label = R.string.strokes,
            value = values.totalStrokes,
            message = state.messageFor(RoundField.TotalStrokes),
            hint = null,
            onValueChange = actions.setStrokes,
            onStep = actions.stepStrokes,
            focusRequester = focus.getValue(RoundField.TotalStrokes),
            last = state.stats.isEmpty(),
        )
        state.stats.forEachIndexed { index, field ->
            NumberInput(
                label = field.label,
                value = values.stat(field),
                message = state.messageFor(RoundField.of(field)),
                hint = maxFor(field, values)?.let { stringResource(R.string.out_of, it) },
                onValueChange = { actions.setStat(field, it) },
                onStep = { actions.stepStat(field, it) },
                focusRequester = focus.getValue(RoundField.of(field)),
                last = index == state.stats.lastIndex,
            )
        }
        if (state.me.let { it is Load.Loaded && it.value.trackingMode != UserResponse.TrackingMode.Detailed }) {
            Text(
                stringResource(R.string.more_stats_hint),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        OutlinedTextField(
            value = values.notes,
            onValueChange = actions.setNotes,
            label = { Text(stringResource(R.string.notes)) },
            minLines = 3,
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences),
            modifier = Modifier.fillMaxWidth().focusRequester(focus.getValue(RoundField.Notes)),
        )

        state.problem?.let {
            Text(
                stringResource(if (it == FormProblem.SaveFailed) R.string.round_save_failed else R.string.auth_cant_connect),
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        if (!online) {
            Text(
                stringResource(R.string.offline_cant_save_round),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        extraContent()
        val saving = stringResource(R.string.saving)
        Button(
            onClick = actions.save,
            enabled = online && !state.saving,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) {
            if (state.saving) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp).semantics { contentDescription = saving },
                    strokeWidth = 2.dp,
                )
            } else {
                Text(stringResource(saveLabel))
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DateField(
    date: LocalDate,
    onChange: (LocalDate) -> Unit,
) {
    var picking by rememberSaveable { mutableStateOf(false) }
    OutlinedTextField(
        value = formatDate(date.toString()),
        onValueChange = {},
        readOnly = true,
        label = { Text(stringResource(R.string.date)) },
        trailingIcon = {
            IconButton(onClick = { picking = true }) {
                Icon(Icons.Filled.DateRange, contentDescription = stringResource(R.string.choose_date))
            }
        },
        modifier = Modifier.fillMaxWidth(),
    )
    if (picking) {
        // The picker works in UTC milliseconds; the date itself has no time zone.
        val picker = rememberDatePickerState(initialSelectedDateMillis = date.atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli())
        DatePickerDialog(
            onDismissRequest = { picking = false },
            confirmButton = {
                TextButton(onClick = {
                    picker.selectedDateMillis?.let { onChange(Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate()) }
                    picking = false
                }) { Text(stringResource(R.string.ok)) }
            },
            dismissButton = { TextButton(onClick = { picking = false }) { Text(stringResource(R.string.cancel)) } },
        ) { DatePicker(state = picker) }
    }
}

@Composable
private fun HolesField(
    holes: Int,
    onChange: (Int) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(stringResource(R.string.holes), style = MaterialTheme.typography.labelLarge)
        SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
            listOf(9, 18).forEachIndexed { index, option ->
                SegmentedButton(
                    selected = holes == option,
                    onClick = { onChange(option) },
                    shape = SegmentedButtonDefaults.itemShape(index, 2),
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget),
                ) { Text(stringResource(R.string.holes_option, option)) }
            }
        }
    }
}

@Composable
private fun TextInput(
    @StringRes label: Int,
    value: String,
    onValueChange: (String) -> Unit,
    focusRequester: FocusRequester,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(stringResource(label)) },
        singleLine = true,
        keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Words, imeAction = ImeAction.Next),
        modifier = Modifier.fillMaxWidth().focusRequester(focusRequester),
    )
}

/** A whole-number field with − and + either side, labelled for TalkBack. */
@Composable
private fun NumberInput(
    @StringRes label: Int,
    value: String,
    message: FieldMessage?,
    hint: String?,
    onValueChange: (String) -> Unit,
    onStep: (Int) -> Unit,
    focusRequester: FocusRequester,
    last: Boolean,
) {
    val name = stringResource(label)
    val decrease = stringResource(R.string.decrease, name)
    val increase = stringResource(R.string.increase, name)
    Row(
        horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm),
        verticalAlignment = Alignment.Top,
    ) {
        OutlinedIconButton(
            onClick = { onStep(-1) },
            modifier =
                Modifier
                    .padding(top = DesignTokens.Spacing.sm)
                    .size(DesignTokens.touchTarget)
                    .semantics { contentDescription = decrease },
        ) { Text("−", modifier = Modifier.clearAndSetSemantics {}) }
        OutlinedTextField(
            value = value,
            onValueChange = onValueChange,
            label = { Text(name) },
            isError = message != null,
            supportingText = (message?.text() ?: hint)?.let { { Text(it) } },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = if (last) ImeAction.Done else ImeAction.Next),
            modifier = Modifier.weight(1f).widthIn(min = 96.dp).focusRequester(focusRequester),
        )
        OutlinedIconButton(
            onClick = { onStep(1) },
            modifier =
                Modifier
                    .padding(top = DesignTokens.Spacing.sm)
                    .size(DesignTokens.touchTarget)
                    .semantics { contentDescription = increase },
        ) { Text("+", modifier = Modifier.clearAndSetSemantics {}) }
    }
}

@Composable
fun FieldMessage.text(): String =
    when (this) {
        is FieldMessage.FromApi -> text
        is FieldMessage.Rule ->
            when (val e = error) {
                RoundError.StrokesRequired -> stringResource(R.string.strokes_required)
                RoundError.WholeNumber -> stringResource(R.string.whole_number)
                RoundError.StrokesPositive -> stringResource(R.string.strokes_positive)
                RoundError.NotNegative -> stringResource(R.string.not_negative)
                is RoundError.AtMostHoles -> stringResource(R.string.at_most_holes, e.max)
                is RoundError.AtMostStrokes -> stringResource(R.string.at_most_strokes, e.max)
            }
    }
