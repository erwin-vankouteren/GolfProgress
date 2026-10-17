package com.golfprogress.app.testing

import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsNode
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import androidx.compose.ui.test.SemanticsNodeInteraction
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp

/**
 * Checks every control under this node, as TalkBack would meet it (#120):
 * - it has a name to read out: text, a content description, or (for a text field) a label;
 * - its touch target is at least 48 × 48 dp (DesignTokens.touchTarget, checklist item 8).
 *
 * Colour contrast is checked on the design tokens themselves (design/build-tokens.mjs).
 * Throws an AssertionError listing every control that fails.
 */
fun SemanticsNodeInteraction.assertControlsAccessible(density: Density) {
    val problems = mutableListOf<String>()
    val minimum = with(density) { 48.dp.toPx() } - 0.5f // rounding

    fun visit(node: SemanticsNode) {
        val config = node.config
        val disabled = config.contains(SemanticsProperties.Disabled)
        val actionable = config.contains(SemanticsActions.OnClick) || config.contains(SemanticsActions.SetText)
        if (actionable && !disabled) {
            val name =
                listOfNotNull(
                    config.getOrNull(SemanticsProperties.ContentDescription)?.joinToString(),
                    config.getOrNull(SemanticsProperties.Text)?.joinToString(),
                ).joinToString().trim()
            val where = name.ifEmpty { "a ${config.getOrNull(SemanticsProperties.Role) ?: "control"} at ${node.boundsInRoot}" }
            if (name.isEmpty()) problems += "$where has no name for TalkBack"
            // Scrolled out of view, a control's visible bounds are empty: judge its laid-out
            // size then, which is what it has once scrolled to.
            val touch = node.touchBoundsInRoot
            val (width, height) =
                if (node.boundsInRoot.isEmpty) node.size.width.toFloat() to node.size.height.toFloat() else touch.width to touch.height
            if (width < minimum || height < minimum) {
                problems +=
                    "$where: touch target ${(width / density.density).toInt()} × ${(height / density.density).toInt()} dp, under 48 × 48"
            }
        }
        node.children.forEach(::visit)
    }

    visit(fetchSemanticsNode())
    if (problems.isNotEmpty()) throw AssertionError("Accessibility problems:\n" + problems.joinToString("\n"))
}
