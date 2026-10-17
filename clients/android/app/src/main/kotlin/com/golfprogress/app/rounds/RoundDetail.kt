package com.golfprogress.app.rounds

import com.golfprogress.api.generated.models.FitnessScoreResponse
import com.golfprogress.api.generated.models.RoundResponse
import com.golfprogress.api.generated.models.UserResponse
import com.golfprogress.app.ui.common.Pillar
import java.time.LocalDate
import kotlin.math.roundToInt

// What the round detail (#97) says about a round, without Compose: why each pillar
// scored what it did, which stats were tracked, and how editing starts. The same as the
// web app's roundDetail.ts.

/** A sentence about a pillar; the screen turns it into text. */
sealed interface Sentence {
    data object FirstRound : Sentence

    /** [difference] is per18 minus the average: negative is better. */
    data class ComparedWithAverage(
        val strokes: Int,
        val average: Double,
        val difference: Double,
    ) : Sentence

    data class NineHoleEquivalent(
        val strokes: Int,
        val per18: Int,
    ) : Sentence

    data object DamageNotTracked : Sentence

    data class Damage(
        val penalties: Int?,
        val blowUps: Int?,
    ) : Sentence

    data object ConsistencyNeedsRounds : Sentence

    data object ConsistencyAtOrBetter : Sentence

    data class ConsistencyWorse(
        val worseBy: Double,
        val spread: Double,
    ) : Sentence

    data object HabitsUnknown : Sentence

    data class Habits(
        val rounds: Int,
        val practice: Int,
    ) : Sentence

    data object Reflected : Sentence

    data object NotReflected : Sentence
}

/**
 * Why each pillar scored what it did, from the round and what its score was judged on
 * (ProgressComputationService.ComputePillars). Strokes are compared per 18 holes, as the
 * scorer does, so a 9-hole round is explained on the same footing.
 */
fun explainPillars(
    round: RoundResponse,
    score: FitnessScoreResponse,
): Map<Pillar, List<Sentence>> {
    val explanation = score.explanation
    val average = explanation.baselineAvgScore
    val spread = explanation.baselineStdDev
    val per18 = if (round.holes == 9) round.totalStrokes * 2 else round.totalStrokes
    val nineHoles = if (round.holes == 9) listOf(Sentence.NineHoleEquivalent(round.totalStrokes, per18)) else emptyList()

    val execution =
        if (average == null) {
            listOf(Sentence.FirstRound)
        } else {
            listOf(Sentence.ComparedWithAverage(per18, average, round1(per18 - average))) + nineHoles
        }

    val damageControl =
        if (round.penalties == null && round.blowUpHoles == null) {
            listOf(Sentence.DamageNotTracked)
        } else {
            listOf(Sentence.Damage(round.penalties, round.blowUpHoles))
        }

    val consistency =
        when {
            average == null || spread == null || spread <= 0 -> listOf(Sentence.ConsistencyNeedsRounds)
            per18 <= average -> listOf(Sentence.ConsistencyAtOrBetter)
            else -> listOf(Sentence.ConsistencyWorse(round1(per18 - average), round1(spread)))
        }

    val rounds = explanation.roundsInHabitWindow
    val habits =
        if (rounds == null) {
            listOf(Sentence.HabitsUnknown)
        } else {
            listOfNotNull(
                Sentence.Habits(rounds, explanation.practiceSessionsInHabitWindow ?: 0),
                explanation.reflected?.let { if (it) Sentence.Reflected else Sentence.NotReflected },
            )
        }

    return mapOf(
        Pillar.Execution to execution,
        Pillar.DamageControl to damageControl,
        Pillar.Consistency to consistency,
        Pillar.Habits to habits,
    )
}

/** A stat's value on a saved round. */
fun RoundResponse.stat(field: StatField): Int? =
    when (field) {
        StatField.TotalPutts -> totalPutts
        StatField.Penalties -> penalties
        StatField.FairwaysHit -> fairwaysHit
        StatField.Gir -> gir
        StatField.ThreePutts -> threePutts
        StatField.BlowUpHoles -> blowUpHoles
    }

/** The stats the round has values for, in form order. */
fun trackedStats(round: RoundResponse): List<StatField> = StatField.entries.filter { round.stat(it) != null }

/**
 * The stats the edit form shows: the tracking mode's, plus any the round already has. A
 * stat the user tracked must stay visible, or saving would quietly clear it.
 */
fun statFieldsForEdit(
    mode: UserResponse.TrackingMode?,
    round: RoundResponse,
): List<StatField> {
    val shown = statFieldsFor(mode).toSet() + trackedStats(round)
    return StatField.entries.filter { it in shown }
}

/** The form, filled in from a saved round. */
fun formValuesFrom(round: RoundResponse) =
    RoundFormValues(
        playedAt = LocalDate.parse(round.playedAt),
        holes = if (round.holes == 9) 9 else 18,
        courseName = round.courseName.orEmpty(),
        teeName = round.teeName.orEmpty(),
        totalStrokes = round.totalStrokes.toString(),
        stats = StatField.entries.associateWith { round.stat(it)?.toString().orEmpty() },
        notes = round.notes.orEmpty(),
    )

private fun round1(value: Double) = (value * 10).roundToInt() / 10.0
