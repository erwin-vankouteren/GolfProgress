import { describe, expect, it } from "vitest";
import type { Schemas } from "../api";
import { explainPillars, formValuesFrom, statFieldsForEdit, trackedStats } from "./roundDetail";

const round = (overrides: Partial<Schemas["RoundResponse"]> = {}): Schemas["RoundResponse"] => ({
  roundId: "r1",
  userId: "u1",
  playedAt: "2026-05-03",
  holes: 18,
  courseName: "Hilversum",
  teeName: null,
  totalStrokes: 88,
  totalPutts: null,
  penalties: null,
  fairwaysHit: null,
  gir: null,
  threePutts: null,
  blowUpHoles: null,
  source: "Manual",
  notes: null,
  createdAt: "",
  updatedAt: "",
  fitnessScore: 72,
  fitnessConfidence: "High",
  ...overrides,
});

const score = (explanation: Partial<Schemas["FitnessScoreExplanation"]> = {}): Schemas["FitnessScoreResponse"] => ({
  fitnessScoreId: "s1",
  userId: "u1",
  roundId: "r1",
  scoreTotal: 72,
  execution: 15,
  damageControl: 21,
  consistency: 25,
  habits: 11,
  confidenceLevel: "High",
  computedAt: "",
  explanation: {
    baselineAvgScore: 91,
    baselineStdDev: 3,
    roundsInHabitWindow: 3,
    practiceSessionsInHabitWindow: 1,
    reflected: false,
    ...explanation,
  },
});

describe("pillar explanations", () => {
  it("compare the strokes with the recent average", () => {
    expect(explainPillars(round(), score()).execution).toEqual([
      { key: "comparedWithAverage", strokes: 88, average: 91, difference: -3 },
    ]);
  });

  it("compare a 9-hole round per 18 holes, and say so", () => {
    expect(explainPillars(round({ holes: 9, totalStrokes: 46 }), score()).execution).toEqual([
      { key: "comparedWithAverage", strokes: 92, average: 91, difference: 1 },
      { key: "nineHoleEquivalent", strokes: 46, per18: 92 },
    ]);
  });

  it("say when there was nothing to compare with", () => {
    const explained = explainPillars(round(), score({ baselineAvgScore: null, baselineStdDev: null }));
    expect(explained.execution).toEqual([{ key: "firstRound" }]);
    expect(explained.consistency).toEqual([{ key: "consistencyNeedsRounds" }]);
  });

  it("list the damage that was tracked, and say what wasn't", () => {
    expect(explainPillars(round(), score()).damageControl).toEqual([{ key: "damageNotTracked" }]);
    expect(explainPillars(round({ penalties: 2 }), score()).damageControl).toEqual([
      { key: "damage", penalties: 2, blowUps: null },
    ]);
  });

  it("give full consistency marks at or better than the average, and say how far off otherwise", () => {
    expect(explainPillars(round(), score()).consistency).toEqual([{ key: "consistencyAtOrBetter" }]);
    expect(explainPillars(round({ totalStrokes: 95 }), score()).consistency).toEqual([
      { key: "consistencyWorse", worseBy: 4, spread: 3 },
    ]);
  });

  it("count the habits, and whether the round was reflected on", () => {
    expect(explainPillars(round(), score()).habits).toEqual([
      { key: "habits", rounds: 3, practice: 1 },
      { key: "notReflected" },
    ]);
    expect(explainPillars(round(), score({ roundsInHabitWindow: null })).habits).toEqual([{ key: "habitsUnknown" }]);
  });
});

describe("editing", () => {
  it("lists the stats the round tracked, in form order", () => {
    expect(trackedStats(round({ blowUpHoles: 1, totalPutts: 33 }))).toEqual(["totalPutts", "blowUpHoles"]);
  });

  it("keeps a tracked stat in the form even when the tracking mode no longer shows it", () => {
    expect(statFieldsForEdit("Minimal", round({ gir: 6 }))).toEqual(["gir"]);
    expect(statFieldsForEdit("Balanced", round({ gir: 6 }))).toEqual(["totalPutts", "penalties", "gir"]);
  });

  it("fills the form from the round", () => {
    expect(formValuesFrom(round({ totalPutts: 33, notes: "windy" }))).toMatchObject({
      playedAt: "2026-05-03",
      holes: 18,
      courseName: "Hilversum",
      teeName: "",
      totalStrokes: "88",
      totalPutts: "33",
      penalties: "",
      notes: "windy",
    });
  });
});
