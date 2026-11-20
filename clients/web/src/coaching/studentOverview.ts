import type { Schemas } from "../api";

/** The student overview's logic (#203), apart from the page so it can be tested on its own. */

export type StudentOverview = Schemas["StudentOverviewResponse"];

export type FitnessTrend = { first: number; last: number; rounds: number };

/**
 * The fitness score of the oldest and the newest scored round among the recent ones, for
 * the trend's one-sentence summary. Undefined with fewer than two scored rounds: one
 * score isn't a trend.
 */
export function fitnessTrend(rounds: StudentOverview["recentRounds"]): FitnessTrend | undefined {
  const scores = rounds.flatMap((round) => (round.fitnessScoreTotal === null ? [] : [round.fitnessScoreTotal]));
  const [first, last] = [scores[0], scores.at(-1)];
  if (scores.length < 2 || first === undefined || last === undefined) return undefined;
  return { first, last, rounds: scores.length };
}

export type ReflectionsState = "notShared" | "none" | "some";

/** Whether the coach sees reflections, and whether there are any yet. */
export function reflectionsState(overview: StudentOverview): ReflectionsState {
  if (!overview.shareReflections) return "notShared";
  return overview.latestReflections.length > 0 ? "some" : "none";
}
