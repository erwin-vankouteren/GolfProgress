import { describe, expect, it } from "vitest";
import {
  checkFocus,
  customRangeIsValid,
  defaultReviewMonth,
  hasMonthlyTrend,
  monthlySummary,
  rangeQuery,
  roundsShortOfTrend,
  strokesLeaveOutRounds,
  timelineChart,
  timelineSummary,
  type MonthPoint,
  type TimelinePoint,
} from "./progress";

const point = (playedAt: string, overrides: Partial<TimelinePoint> = {}): TimelinePoint => ({
  roundId: playedAt,
  playedAt,
  holes: 18,
  totalStrokes: 90,
  totalPutts: null,
  penalties: null,
  threePutts: null,
  blowUpHoles: null,
  fitnessScoreTotal: 60,
  fitnessConfidence: "Low",
  microWinTitle: null,
  ...overrides,
});

const month = (
  monthStart: string,
  fitnessAvg: number | null,
  roundsLogged = fitnessAvg === null ? 0 : 2,
): MonthPoint => ({
  monthStart,
  fitnessAvg,
  fitnessChange: null,
  roundsLogged,
  highlights: [],
  hasReview: false,
});

describe("rangeQuery (#107)", () => {
  const none = { from: "", to: "" };

  it("counts back whole months from today", () => {
    expect(rangeQuery("3m", "2026-10-17", none)).toEqual({ from: "2026-07-17" });
    expect(rangeQuery("12m", "2026-10-17", none)).toEqual({ from: "2025-10-17" });
  });

  it("ends on the month's last day when it's shorter", () => {
    expect(rangeQuery("3m", "2026-05-31", none)).toEqual({ from: "2026-02-28" });
  });

  it("leaves all time and empty custom ends open", () => {
    expect(rangeQuery("all", "2026-10-17", none)).toEqual({});
    expect(rangeQuery("custom", "2026-10-17", { from: "2026-01-01", to: "" })).toEqual({ from: "2026-01-01" });
  });

  it("refuses a custom range that ends before it starts", () => {
    expect(customRangeIsValid({ from: "2026-05-01", to: "2026-04-01" })).toBe(false);
    expect(customRangeIsValid({ from: "2026-05-01", to: "2026-05-01" })).toBe(true);
    expect(customRangeIsValid({ from: "", to: "2026-04-01" })).toBe(true);
  });
});

describe("timelineChart", () => {
  it("draws strokes for 18-hole rounds only when holes are mixed", () => {
    const points = [point("2026-05-01"), point("2026-05-02", { holes: 9, totalStrokes: 45 })];
    const { rows, strokesHoles } = timelineChart(points);

    expect(strokesHoles).toBe(18);
    expect(rows.map((r) => r.strokes)).toEqual([90, null]);
    expect(rows.map((r) => r.fitness)).toEqual([60, 60]);
    expect(strokesLeaveOutRounds(points)).toBe(true);
  });

  it("draws 9-hole strokes when there are no 18-hole rounds", () => {
    const points = [point("2026-05-01", { holes: 9, totalStrokes: 45 })];
    expect(timelineChart(points).strokesHoles).toBe(9);
    expect(strokesLeaveOutRounds(points)).toBe(false);
  });
});

describe("timelineSummary", () => {
  it("goes from the first to the last scored round", () => {
    const summary = timelineSummary([
      point("2026-05-01", { fitnessScoreTotal: null }),
      point("2026-05-08", { fitnessScoreTotal: 50 }),
      point("2026-05-15", { fitnessScoreTotal: 71 }),
    ]);
    expect(summary).toEqual({
      rounds: 3,
      firstDate: "2026-05-01",
      lastDate: "2026-05-15",
      score: { first: 50, last: 71, average: 61 },
    });
  });

  it("is null without rounds, and has no score without scored rounds", () => {
    expect(timelineSummary([])).toBeNull();
    expect(timelineSummary([point("2026-05-01", { fitnessScoreTotal: null })])?.score).toBeNull();
  });

  it("needs three scored rounds for a trend", () => {
    expect(roundsShortOfTrend([point("2026-05-01")])).toBe(2);
    expect(roundsShortOfTrend([point("2026-05-01"), point("2026-05-02"), point("2026-05-03")])).toBe(0);
    expect(roundsShortOfTrend([point("2026-05-01", { fitnessScoreTotal: null })])).toBe(3);
  });
});

describe("monthly trend", () => {
  const items = [month("2026-08-01", 58.4), month("2026-09-01", null), month("2026-10-01", 64.6)];

  it("summarises from the first to the last month with an average", () => {
    expect(monthlySummary(items)).toEqual({
      months: 2,
      first: { month: "2026-08-01", average: 58 },
      last: { month: "2026-10-01", average: 65 },
    });
    expect(hasMonthlyTrend(items)).toBe(true);
  });

  it("needs two months with an average", () => {
    expect(hasMonthlyTrend([month("2026-10-01", 60)])).toBe(false);
    expect(monthlySummary([month("2026-10-01", null)])).toBeNull();
  });

  it("opens the review on the latest month with rounds, else this month", () => {
    expect(defaultReviewMonth([month("2026-09-01", 60), month("2026-10-01", null)])).toBe("2026-09-01");
    expect(defaultReviewMonth([month("2026-09-01", null), month("2026-10-01", null)])).toBe("2026-10-01");
    expect(defaultReviewMonth([])).toBeUndefined();
  });
});

describe("checkFocus", () => {
  it("trims, sends blank as nothing, and bounds the length", () => {
    expect(checkFocus("  Fewer three-putts ")).toEqual({ ok: true, value: "Fewer three-putts" });
    expect(checkFocus("   ")).toEqual({ ok: true, value: undefined });
    expect(checkFocus("x".repeat(200)).ok).toBe(true);
    expect(checkFocus("x".repeat(201)).ok).toBe(false);
    // Counted as people count characters: 200 emoji are 200, not 400.
    expect(checkFocus("🏌".repeat(200)).ok).toBe(true);
  });
});
