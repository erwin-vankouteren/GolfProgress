import { describe, expect, it } from "vitest";
import { fitnessTrend, reflectionsState, type StudentOverview } from "./studentOverview";

const round = (fitnessScoreTotal: number | null) => ({ fitnessScoreTotal }) as StudentOverview["recentRounds"][number];

describe("fitnessTrend (#203)", () => {
  it("runs from the oldest to the newest scored round, skipping unscored ones", () => {
    expect(fitnessTrend([round(null), round(58), round(null), round(61), round(66)])).toEqual({
      first: 58,
      last: 66,
      rounds: 3,
    });
  });

  it("needs two scored rounds", () => {
    expect(fitnessTrend([])).toBeUndefined();
    expect(fitnessTrend([round(60), round(null)])).toBeUndefined();
  });
});

describe("reflectionsState (#203)", () => {
  const overview = (shareReflections: boolean, count: number) =>
    ({ shareReflections, latestReflections: Array.from({ length: count }, () => ({})) }) as StudentOverview;

  it("tells not shared, none yet and some apart", () => {
    expect(reflectionsState(overview(false, 0))).toBe("notShared");
    expect(reflectionsState(overview(true, 0))).toBe("none");
    expect(reflectionsState(overview(true, 2))).toBe("some");
  });
});
