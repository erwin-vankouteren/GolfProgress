import type { Schemas } from "../api";

/**
 * The Progress section's logic (#107), kept out of the components so it can be tested
 * on its own: the date ranges, what the charts plot, and when there's enough to show a
 * trend.
 */

export type TimelinePoint = Schemas["RoundTimelinePointResponse"];
export type MonthPoint = Schemas["MonthlyTimelinePointResponse"];

/** Rounds the timeline asks for: the API's maximum. */
export const timelineTake = 100;

/** Months in the monthly trend, and in the review's month picker. */
export const trendMonths = 12;

/** Scored rounds needed before the timeline draws a line. */
export const trendMinimumRounds = 3;

/** Months with an average needed before the monthly trend draws bars. */
export const trendMinimumMonths = 2;

export const rangeKeys = ["3m", "6m", "12m", "all", "custom"] as const;
export type RangeKey = (typeof rangeKeys)[number];

/** A date as the API writes it, yyyy-MM-dd, read in UTC like every API date. */
export const isoDate = (date: Date) => date.toISOString().slice(0, 10);

/** Today's date in UTC, as the API counts days. */
export const todayIso = (now: Date = new Date()) => isoDate(now);

function monthsBefore(today: string, months: number): string {
  const date = new Date(`${today}T00:00:00Z`);
  const day = date.getUTCDate();
  date.setUTCDate(1);
  date.setUTCMonth(date.getUTCMonth() - months);
  // 31 May minus three months is 28 or 29 February, not 3 March.
  const lastDay = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
  date.setUTCDate(Math.min(day, lastDay));
  return isoDate(date);
}

export type CustomRange = { from: string; to: string };

/** GET /me/timeline's from and to for a range; left out where the range is open. */
export function rangeQuery(key: RangeKey, today: string, custom: CustomRange): { from?: string; to?: string } {
  switch (key) {
    case "3m":
      return { from: monthsBefore(today, 3) };
    case "6m":
      return { from: monthsBefore(today, 6) };
    case "12m":
      return { from: monthsBefore(today, 12) };
    case "all":
      return {};
    case "custom":
      return {
        ...(custom.from ? { from: custom.from } : {}),
        ...(custom.to ? { to: custom.to } : {}),
      };
  }
}

/** A custom range ending before it starts can't be asked for. */
export const customRangeIsValid = ({ from, to }: CustomRange) => !from || !to || from <= to;

export type TimelineRow = {
  roundId: string;
  playedAt: string;
  fitness: number | null;
  /** Only for rounds of `strokesHoles` holes, so 9 and 18 aren't drawn on one line. */
  strokes: number | null;
};

/**
 * What the timeline chart plots, oldest first. Strokes for 9 and 18 holes aren't
 * comparable, so the strokes line shows 18-hole rounds, or 9-hole ones when there are
 * no 18-hole rounds. Every round stays in the table.
 */
export function timelineChart(points: TimelinePoint[]): { rows: TimelineRow[]; strokesHoles: 9 | 18 | null } {
  const strokesHoles = points.some((p) => p.holes === 18) ? 18 : points.some((p) => p.holes === 9) ? 9 : null;
  return {
    strokesHoles,
    rows: points.map((p) => ({
      roundId: p.roundId,
      playedAt: p.playedAt,
      fitness: p.fitnessScoreTotal,
      strokes: p.holes === strokesHoles ? p.totalStrokes : null,
    })),
  };
}

/** Whether the strokes line leaves out some rounds, which the chart then says. */
export const strokesLeaveOutRounds = (points: TimelinePoint[]) => new Set(points.map((p) => p.holes)).size > 1;

export type TimelineSummary = {
  rounds: number;
  firstDate: string;
  lastDate: string;
  /** From the first to the last scored round; null when none is scored. */
  score: { first: number; last: number; average: number } | null;
};

/** The sentence that stands in for the timeline chart. Null when there are no rounds. */
export function timelineSummary(points: TimelinePoint[]): TimelineSummary | null {
  const [firstPoint, lastPoint] = [points.at(0), points.at(-1)];
  if (!firstPoint || !lastPoint) return null;
  const scores = points.map((p) => p.fitnessScoreTotal).filter((s): s is number => s !== null);
  const [first, last] = [scores.at(0), scores.at(-1)];
  return {
    rounds: points.length,
    firstDate: firstPoint.playedAt,
    lastDate: lastPoint.playedAt,
    score:
      first === undefined || last === undefined
        ? null
        : { first, last, average: Math.round(scores.reduce((sum, s) => sum + s, 0) / scores.length) },
  };
}

/** How many more scored rounds before there's a trend to draw; 0 when there is one. */
export const roundsShortOfTrend = (points: TimelinePoint[]) =>
  Math.max(0, trendMinimumRounds - points.filter((p) => p.fitnessScoreTotal !== null).length);

export type MonthlySummary = {
  months: number;
  first: { month: string; average: number };
  last: { month: string; average: number };
};

/** The sentence that stands in for the monthly chart. Null without any month's average. */
export function monthlySummary(items: MonthPoint[]): MonthlySummary | null {
  const averaged = items.flatMap((i) => (i.fitnessAvg === null ? [] : [{ month: i.monthStart, avg: i.fitnessAvg }]));
  const [first, last] = [averaged.at(0), averaged.at(-1)];
  if (!first || !last) return null;
  return {
    months: averaged.length,
    first: { month: first.month, average: Math.round(first.avg) },
    last: { month: last.month, average: Math.round(last.avg) },
  };
}

/** Whether enough months have an average for the monthly chart. */
export const hasMonthlyTrend = (items: MonthPoint[]) =>
  items.filter((i) => i.fitnessAvg !== null).length >= trendMinimumMonths;

/**
 * The month the review opens on: the latest month with rounds, or this month when
 * there are none in the trend's months.
 */
export function defaultReviewMonth(items: MonthPoint[]): string | undefined {
  const played = items.filter((i) => i.roundsLogged > 0);
  return (played.length ? played[played.length - 1] : items[items.length - 1])?.monthStart;
}

/** The next month's first day, yyyy-MM-01: what a review's focus is for. */
export function nextMonth(monthStart: string): string {
  const date = new Date(`${monthStart}T00:00:00Z`);
  date.setUTCMonth(date.getUTCMonth() + 1);
  return isoDate(date);
}

/** Matches the API's GenerateMonthlyReviewRequest.FocusNextMonthMaxLength. */
export const maxFocusLength = 200;

/** The focus as the API is sent it, or why it can't be. Counted as people count characters. */
export function checkFocus(focus: string): { ok: true; value: string | undefined } | { ok: false } {
  const trimmed = focus.trim();
  if ([...trimmed].length > maxFocusLength) return { ok: false };
  return { ok: true, value: trimmed === "" ? undefined : trimmed };
}
