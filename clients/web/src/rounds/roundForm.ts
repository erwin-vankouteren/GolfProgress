import type { Schemas } from "../api";

/**
 * The Log round form's rules (#92), without React: which fields a tracking mode shows,
 * validation with the API's rules (CreateRoundRequest, #11), and the request it sends.
 */

export type TrackingMode = Schemas["UserResponse"]["trackingMode"];

export type StatField = "totalPutts" | "penalties" | "fairwaysHit" | "gir" | "threePutts" | "blowUpHoles";

export type NumberField = "totalStrokes" | StatField;

export type RoundField = "playedAt" | "holes" | "courseName" | "teeName" | "notes" | NumberField;

/** What the inputs hold: text, so a half-typed or cleared number is representable. */
export interface RoundFormValues {
  playedAt: string;
  holes: 9 | 18;
  courseName: string;
  teeName: string;
  totalStrokes: string;
  totalPutts: string;
  penalties: string;
  fairwaysHit: string;
  gir: string;
  threePutts: string;
  blowUpHoles: string;
  notes: string;
}

/**
 * The optional stats each tracking mode shows (docs/clients/ux-design.md → "Rounds"):
 * Minimal none, Balanced putts and penalties, Detailed all six. Unknown modes show the
 * most, so nothing a user tracks is ever hidden.
 */
export function statFieldsFor(mode: TrackingMode | undefined): StatField[] {
  switch (mode) {
    case "Minimal":
      return [];
    case "Balanced":
      return ["totalPutts", "penalties"];
    default:
      return ["totalPutts", "penalties", "fairwaysHit", "gir", "threePutts", "blowUpHoles"];
  }
}

/** Stats counted at most once per hole, so capped at the hole count. */
const perHole: ReadonlySet<StatField> = new Set(["fairwaysHit", "gir", "threePutts", "blowUpHoles"]);

/** Stats that are part of the total, so capped at the strokes. */
const partOfStrokes: ReadonlySet<StatField> = new Set(["totalPutts", "penalties"]);

/** The most a stat can be with the current values, for the "/18" hint and the stepper. */
export function maxFor(field: StatField, values: RoundFormValues): number | undefined {
  if (perHole.has(field)) return values.holes;
  const strokes = parseWhole(values.totalStrokes);
  return partOfStrokes.has(field) && strokes !== undefined && strokes > 0 ? strokes : undefined;
}

export type RoundError =
  | { code: "dateRequired" }
  | { code: "strokesRequired" }
  | { code: "wholeNumber" }
  | { code: "strokesPositive" }
  | { code: "notNegative" }
  | { code: "atMostHoles"; max: number }
  | { code: "atMostStrokes"; max: number };

export type RoundErrors = Partial<Record<RoundField, RoundError>>;

/** The API's rules, checked on the visible fields. */
export function validateRound(values: RoundFormValues, stats: StatField[]): RoundErrors {
  const errors: RoundErrors = {};

  if (!/^\d{4}-\d{2}-\d{2}$/.test(values.playedAt)) errors.playedAt = { code: "dateRequired" };

  const strokes = parseWhole(values.totalStrokes);
  if (values.totalStrokes.trim() === "") errors.totalStrokes = { code: "strokesRequired" };
  else if (strokes === undefined) errors.totalStrokes = { code: "wholeNumber" };
  else if (strokes <= 0) errors.totalStrokes = { code: "strokesPositive" };

  for (const field of stats) {
    const text = values[field].trim();
    if (text === "") continue; // not tracked this round

    const value = parseWhole(text);
    if (value === undefined) errors[field] = { code: "wholeNumber" };
    else if (value < 0) errors[field] = { code: "notNegative" };
    else if (perHole.has(field) && value > values.holes) errors[field] = { code: "atMostHoles", max: values.holes };
    else if (partOfStrokes.has(field) && strokes !== undefined && strokes > 0 && value > strokes) {
      errors[field] = { code: "atMostStrokes", max: strokes };
    }
  }

  return errors;
}

/** The POST /me/rounds body. Stats the form doesn't show, or left empty, are not tracked. */
export function toRequest(values: RoundFormValues, stats: StatField[]): Schemas["CreateRoundRequest"] {
  const stat = (field: StatField) => (stats.includes(field) ? (parseWhole(values[field]) ?? null) : null);

  return {
    playedAt: values.playedAt,
    holes: values.holes,
    courseName: values.courseName.trim() || null,
    teeName: values.teeName.trim() || null,
    totalStrokes: parseWhole(values.totalStrokes)!,
    totalPutts: stat("totalPutts"),
    penalties: stat("penalties"),
    fairwaysHit: stat("fairwaysHit"),
    gir: stat("gir"),
    threePutts: stat("threePutts"),
    blowUpHoles: stat("blowUpHoles"),
    notes: values.notes.trim() || null,
  };
}

/** A new form: today, 18 holes, nothing else filled in. */
export function emptyRound(today: Date = new Date()): RoundFormValues {
  return {
    playedAt: localIsoDate(today),
    holes: 18,
    courseName: "",
    teeName: "",
    totalStrokes: "",
    totalPutts: "",
    penalties: "",
    fairwaysHit: "",
    gir: "",
    threePutts: "",
    blowUpHoles: "",
    notes: "",
  };
}

/**
 * The next value when a stepper is pressed. From empty it starts somewhere useful
 * rather than at zero: strokes at the user's typical score (or 90) for the hole count,
 * putts at two a hole, the rest at zero.
 */
export function step(
  field: NumberField,
  values: RoundFormValues,
  delta: 1 | -1,
  typical18Score: number | null | undefined,
): string {
  const current = parseWhole(values[field]);
  if (current === undefined) return String(startingValue(field, values.holes, typical18Score));

  const max = field === "totalStrokes" ? undefined : maxFor(field, values);
  const min = field === "totalStrokes" ? 1 : 0;
  const next = Math.max(min, current + delta);
  return String(max === undefined ? next : Math.min(max, next));
}

function startingValue(field: NumberField, holes: 9 | 18, typical18Score: number | null | undefined): number {
  if (field === "totalStrokes") return Math.round(((typical18Score ?? 90) * holes) / 18);
  if (field === "totalPutts") return holes * 2;
  return 0;
}

/** A whole number, or undefined for empty or anything else (1.5, "abc", "1e2"). */
export function parseWhole(text: string): number | undefined {
  const trimmed = text.trim();
  return /^-?\d+$/.test(trimmed) ? Number(trimmed) : undefined;
}

/** yyyy-MM-dd in the user's own time zone: "today" is the day on their calendar. */
export function localIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
