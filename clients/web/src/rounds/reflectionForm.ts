import type { Schemas } from "../api";

/**
 * The reflection form's rules (#99), without React. Every field is optional, but a
 * reflection needs at least one; text is bounded as the API bounds it.
 */

/** CreateReflectionRequest.TextMaxLength. */
export const reflectionTextMax = 1000;

export type Rating = 1 | 2 | 3 | 4 | 5;

export type ReflectionText = "whatWorked" | "whatToRepeat" | "oneChangeNext";

export interface ReflectionValues {
  focusRating: Rating | null;
  moodRating: Rating | null;
  /** Null until the user answers: "no" is an answer too. */
  frustrationSpillover: boolean | null;
  whatWorked: string;
  whatToRepeat: string;
  oneChangeNext: string;
}

export const emptyReflection = (): ReflectionValues => ({
  focusRating: null,
  moodRating: null,
  frustrationSpillover: null,
  whatWorked: "",
  whatToRepeat: "",
  oneChangeNext: "",
});

export const textFields: ReflectionText[] = ["whatWorked", "whatToRepeat", "oneChangeNext"];

/** Whether anything is filled in. Spaces alone don't count. */
export function hasContent(values: ReflectionValues): boolean {
  return (
    values.focusRating !== null ||
    values.moodRating !== null ||
    values.frustrationSpillover !== null ||
    textFields.some((field) => values[field].trim() !== "")
  );
}

/** The text fields over the limit. Counted in characters as people see them. */
export function tooLong(values: ReflectionValues): ReflectionText[] {
  return textFields.filter((field) => [...values[field]].length > reflectionTextMax);
}

/** The POST body: trimmed text, and nothing sent for what was left empty. */
export function toReflectionRequest(values: ReflectionValues): Schemas["CreateReflectionRequest"] {
  const text = (value: string) => value.trim() || null;
  return {
    focusRating: values.focusRating,
    moodRating: values.moodRating,
    frustrationSpillover: values.frustrationSpillover,
    whatWorked: text(values.whatWorked),
    whatToRepeat: text(values.whatToRepeat),
    oneChangeNext: text(values.oneChangeNext),
  };
}
