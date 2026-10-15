/**
 * Open enums (#134): response fields the API may add values to at any time. The spec
 * publishes them as plain strings, so a value this build doesn't know never fails a
 * request; map them through these helpers, and show something generic for "unknown".
 *
 * The lists mirror the spec's `x-known-values`; a test fails if they drift apart. All
 * other enum fields are closed and typed directly by the generated schema.
 */

export const knownSessionTypes = ["Range", "Putting", "ShortGame", "Simulator", "Lesson", "Other"] as const;
export const knownMicroWinCategories = ["StatDelta", "Behavior", "Narrative", "Habit", "Recovery", "FocusArea"] as const;
export const knownRoundSources = ["Manual", "Import", "Coach", "Other"] as const;

/** A value from an open enum: one this build knows, or "unknown". */
export type Known<T extends string> = T | "unknown";

export type SessionType = Known<(typeof knownSessionTypes)[number]>;
export type MicroWinCategory = Known<(typeof knownMicroWinCategories)[number]>;
export type RoundSource = Known<(typeof knownRoundSources)[number]>;

function known<T extends string>(values: readonly T[], value: string | null | undefined): Known<T> {
  return values.includes(value as T) ? (value as T) : "unknown";
}

export const sessionTypeOf = (value: string | null | undefined): SessionType => known(knownSessionTypes, value);
export const microWinCategoryOf = (value: string | null | undefined): MicroWinCategory =>
  known(knownMicroWinCategories, value);
export const roundSourceOf = (value: string | null | undefined): RoundSource => known(knownRoundSources, value);
