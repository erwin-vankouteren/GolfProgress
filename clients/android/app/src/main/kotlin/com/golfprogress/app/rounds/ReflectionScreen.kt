package com.golfprogress.app.rounds

import androidx.annotation.StringRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.golfprogress.app.R
import com.golfprogress.app.ui.common.isOnline
import com.golfprogress.app.ui.theme.DesignTokens

/** Add reflection (#101): from a round's detail; [onSaved] returns to it. */
@Composable
fun ReflectionScreen(
    onSaved: () -> Unit,
    viewModel: ReflectionViewModel = hiltViewModel(),
    online: Boolean = isOnline(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state.saved) { if (state.saved) onSaved() }

    Column(
        modifier =
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(DesignTokens.Spacing.lg),
        verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.lg),
    ) {
        Text(
            stringResource(R.string.reflection_habits_hint),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        RatingField(R.string.focus, state.values.focusRating, viewModel::setFocus)
        RatingField(R.string.mood, state.values.moodRating, viewModel::setMood)
        FrustrationField(state.values.frustrationSpillover, viewModel::setFrustration)
        ReflectionText.entries.forEach { field ->
            val error =
                state.apiErrors[field]
                    ?: if (state.tooLong(field)) stringResource(R.string.at_most_characters, REFLECTION_TEXT_MAX) else null
            OutlinedTextField(
                value = state.values.text(field),
                onValueChange = { viewModel.setText(field, it) },
                label = { Text(stringResource(field.label)) },
                isError = error != null,
                supportingText = error?.let { { Text(it) } },
                minLines = 2,
                keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences),
                modifier = Modifier.fillMaxWidth(),
            )
        }

        val problem =
            when {
                state.needsSomething -> R.string.reflection_needs_something
                state.problem == FormProblem.SaveFailed -> R.string.reflection_save_failed
                state.problem == FormProblem.CantConnect -> R.string.auth_cant_connect
                else -> null
            }
        problem?.let {
            Text(
                stringResource(it),
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
            )
        }
        if (!online) {
            Text(
                stringResource(R.string.offline_cant_save_reflection),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Button(
            onClick = viewModel::save,
            enabled = online && !state.saving,
            modifier = Modifier.fillMaxWidth().heightIn(min = DesignTokens.touchTarget),
        ) { Text(stringResource(if (state.saving) R.string.saving else R.string.save_reflection)) }
    }
}

private val ReflectionText.label: Int
    get() =
        when (this) {
            ReflectionText.WhatWorked -> R.string.what_worked
            ReflectionText.WhatToRepeat -> R.string.what_to_repeat
            ReflectionText.OneChangeNext -> R.string.one_change_next
        }

/**
 * A 1–5 rating as five radio buttons, each read as "4 of 5", touch-target sized, and
 * Clear to unset it.
 */
@Composable
private fun RatingField(
    @StringRes label: Int,
    value: Int?,
    onChange: (Int?) -> Unit,
) {
    val name = stringResource(label)
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(name, style = MaterialTheme.typography.labelLarge)
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
            Row(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
                (1..5).forEach { rating ->
                    val description = stringResource(R.string.rating_of_five, name, rating)
                    Choice(
                        text = rating.toString(),
                        selected = value == rating,
                        onClick = { onChange(rating) },
                        description = description,
                        circle = true,
                    )
                }
            }
            if (value != null) {
                val clear = stringResource(R.string.clear_rating, name)
                TextButton(
                    onClick = { onChange(null) },
                    modifier = Modifier.heightIn(min = DesignTokens.touchTarget).semantics { contentDescription = clear },
                ) { Text(stringResource(R.string.clear)) }
            }
        }
    }
}

/** Yes or No, unanswered until chosen. */
@Composable
private fun FrustrationField(
    value: Boolean?,
    onChange: (Boolean) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.xs)) {
        Text(stringResource(R.string.frustration_question), style = MaterialTheme.typography.labelLarge)
        Row(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(DesignTokens.Spacing.sm)) {
            Choice(stringResource(R.string.yes), selected = value == true, onClick = { onChange(true) })
            Choice(stringResource(R.string.no), selected = value == false, onClick = { onChange(false) })
        }
    }
}

@Composable
private fun Choice(
    text: String,
    selected: Boolean,
    onClick: () -> Unit,
    description: String? = null,
    circle: Boolean = false,
) {
    val colours = MaterialTheme.colorScheme
    Surface(
        shape = if (circle) CircleShape else RoundedCornerShape(DesignTokens.Radius.md),
        color = if (selected) colours.primaryContainer else colours.surface,
        contentColor = if (selected) colours.onPrimaryContainer else colours.onSurface,
        border = BorderStroke(if (selected) 2.dp else 1.dp, if (selected) colours.primary else colours.outline),
        modifier =
            Modifier
                .heightIn(min = DesignTokens.touchTarget)
                .let { if (circle) it.size(DesignTokens.touchTarget) else it }
                .selectable(selected = selected, onClick = onClick, role = Role.RadioButton)
                .semantics { if (description != null) contentDescription = description },
    ) {
        Box(contentAlignment = Alignment.Center, modifier = Modifier.padding(horizontal = if (circle) 0.dp else DesignTokens.Spacing.xl)) {
            // A labelled choice ("Focus, 4 of 5") reads its label, not the bare number.
            Text(
                text,
                style = MaterialTheme.typography.labelLarge,
                modifier = if (description != null) Modifier.clearAndSetSemantics {} else Modifier,
            )
        }
    }
}
