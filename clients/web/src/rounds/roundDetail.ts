import type { Schemas } from "../api";
import { statFieldsFor, type RoundFormValues, type StatField, type TrackingMode } from "./roundForm";

/**
 * What the round detail (#95) says about a round, without React: why each pillar scored
 * what it did, which stats were tracked, and how editing starts.
 */

type Round = Schemas["RoundResponse"];
type Score = Schemas["FitnessScoreResponse"];

/** A sentence, as a message key and its values; the page turns it into text. */
export type Sentence =
  | { key: "firstRound" }
  | { key: "comparedWithAverage"; strokes: number; average: number; difference: number }
  | { key: "nineHoleEquivalent"; strokes: number; per18: number }
  | { key: "damageNotTracked" }
  | { key: "damage"; penalties: number | null; blowUps: number | null }
  | { key: "consistencyNeedsRounds" }
  | { key: "consistencyAtOrBetter" }
  | { key: "consistencyWorse"; worseBy: number; spread: number }
  | { key: "habitsUnknown" }
  | { key: "habits"; rounds: number; practice: number }
  | { key: "reflected" }
  | { key: "notReflected" };

export interface PillarExplanations {
  execution: Sentence[];
  damageControl: Sentence[];
  consistency: Sentence[];
  habits: Sentence[];
}

/**
 * Why each pillar scored what it did, from the round and what its score was judged on
 * (ProgressComputationService.ComputePillars). Strokes are compared per 18 holes, as the
 * scorer does, so a 9-hole round is explained on the same footing.
 */
export function explainPillars(round: Round, score: Score): PillarExplanations {
  const { baselineAvgScore: average, baselineStdDev: spread } = score.explanation;
  const per18 = round.holes === 9 ? round.totalStrokes * 2 : round.totalStrokes;
  const nineHoles: Sentence[] =
    round.holes === 9 ? [{ key: "nineHoleEquivalent", strokes: round.totalStrokes, per18 }] : [];

  const execution: Sentence[] =
    average === null
      ? [{ key: "firstRound" }]
      : [{ key: "comparedWithAverage", strokes: per18, average, difference: round1(per18 - average) }, ...nineHoles];

  const damageControl: Sentence[] =
    round.penalties === null && round.blowUpHoles === null
      ? [{ key: "damageNotTracked" }]
      : [{ key: "damage", penalties: round.penalties, blowUps: round.blowUpHoles }];

  const consistency: Sentence[] =
    average === null || spread === null || spread <= 0
      ? [{ key: "consistencyNeedsRounds" }]
      : per18 <= average
        ? [{ key: "consistencyAtOrBetter" }]
        : [{ key: "consistencyWorse", worseBy: round1(per18 - average), spread: round1(spread) }];

  const { roundsInHabitWindow: rounds, practiceSessionsInHabitWindow: practice, reflected } = score.explanation;
  const habits: Sentence[] =
    rounds === null
      ? [{ key: "habitsUnknown" }]
      : [
          { key: "habits", rounds, practice: practice ?? 0 },
          ...(reflected === null ? [] : [{ key: reflected ? "reflected" : "notReflected" } as Sentence]),
        ];

  return { execution, damageControl, consistency, habits };
}

/** The stats the round has values for, in form order. */
export function trackedStats(round: Round): StatField[] {
  return statFieldsFor("Detailed").filter((field) => round[field] !== null);
}

/**
 * The stats the edit form shows: the tracking mode's, plus any the round already has. A
 * stat the user tracked must stay visible, or saving would quietly clear it.
 */
export function statFieldsForEdit(mode: TrackingMode | undefined, round: Round): StatField[] {
  const shown = new Set([...statFieldsFor(mode), ...trackedStats(round)]);
  return statFieldsFor("Detailed").filter((field) => shown.has(field));
}

/** The form, filled in from a saved round. */
export function formValuesFrom(round: Round): RoundFormValues {
  const text = (value: number | null) => (value === null ? "" : String(value));
  return {
    playedAt: round.playedAt,
    holes: round.holes === 9 ? 9 : 18,
    courseName: round.courseName ?? "",
    teeName: round.teeName ?? "",
    totalStrokes: String(round.totalStrokes),
    totalPutts: text(round.totalPutts),
    penalties: text(round.penalties),
    fairwaysHit: text(round.fairwaysHit),
    gir: text(round.gir),
    threePutts: text(round.threePutts),
    blowUpHoles: text(round.blowUpHoles),
    notes: round.notes ?? "",
  };
}

const round1 = (value: number) => Math.round(value * 10) / 10;
