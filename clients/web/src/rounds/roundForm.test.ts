import { describe, expect, it } from "vitest";
import {
  emptyRound,
  localIsoDate,
  maxFor,
  parseWhole,
  statFieldsFor,
  step,
  toRequest,
  validateRound,
  type RoundFormValues,
} from "./roundForm";

const round = (values: Partial<RoundFormValues>): RoundFormValues => ({
  ...emptyRound(new Date(2026, 4, 3)),
  ...values,
});

const allStats = statFieldsFor("Detailed");

describe("fields by tracking mode", () => {
  it("shows no stats in Minimal, putts and penalties in Balanced, and all six in Detailed", () => {
    expect(statFieldsFor("Minimal")).toEqual([]);
    expect(statFieldsFor("Balanced")).toEqual(["totalPutts", "penalties"]);
    expect(statFieldsFor("Detailed")).toEqual([
      "totalPutts",
      "penalties",
      "fairwaysHit",
      "gir",
      "threePutts",
      "blowUpHoles",
    ]);
  });
});

describe("validation, with the API's rules", () => {
  it("accepts a round with only strokes", () => {
    expect(validateRound(round({ totalStrokes: "88" }), [])).toEqual({});
  });

  it("requires strokes above zero, as a whole number", () => {
    expect(validateRound(round({ totalStrokes: "" }), []).totalStrokes?.code).toBe("strokesRequired");
    expect(validateRound(round({ totalStrokes: "0" }), []).totalStrokes?.code).toBe("strokesPositive");
    expect(validateRound(round({ totalStrokes: "88.5" }), []).totalStrokes?.code).toBe("wholeNumber");
  });

  it("requires a date", () => {
    expect(validateRound(round({ playedAt: "", totalStrokes: "88" }), []).playedAt?.code).toBe("dateRequired");
  });

  it("leaves empty stats alone: they weren't tracked", () => {
    expect(validateRound(round({ totalStrokes: "88" }), allStats)).toEqual({});
  });

  it("rejects negative counts", () => {
    expect(validateRound(round({ totalStrokes: "88", penalties: "-1" }), allStats).penalties?.code).toBe("notNegative");
  });

  it("caps fairways, greens, three-putts and blow-ups at the hole count", () => {
    const errors = validateRound(
      round({ holes: 9, totalStrokes: "45", fairwaysHit: "10", gir: "9", threePutts: "10", blowUpHoles: "10" }),
      allStats,
    );

    expect(errors.fairwaysHit).toEqual({ code: "atMostHoles", max: 9 });
    expect(errors.gir).toBeUndefined();
    expect(errors.threePutts).toEqual({ code: "atMostHoles", max: 9 });
    expect(errors.blowUpHoles).toEqual({ code: "atMostHoles", max: 9 });
  });

  it("caps putts and penalties at the strokes", () => {
    const errors = validateRound(round({ totalStrokes: "40", totalPutts: "41", penalties: "40" }), allStats);

    expect(errors.totalPutts).toEqual({ code: "atMostStrokes", max: 40 });
    expect(errors.penalties).toBeUndefined();
  });

  it("only checks the stats on show", () => {
    expect(validateRound(round({ totalStrokes: "88", fairwaysHit: "99" }), statFieldsFor("Balanced"))).toEqual({});
  });
});

describe("the request", () => {
  it("sends numbers, trims text, and leaves hidden or empty stats untracked", () => {
    const values = round({
      courseName: "  Hilversum ",
      teeName: "",
      totalStrokes: "88",
      totalPutts: "33",
      penalties: "",
      fairwaysHit: "7",
      notes: " windy ",
    });

    expect(toRequest(values, statFieldsFor("Balanced"))).toEqual({
      playedAt: "2026-05-03",
      holes: 18,
      courseName: "Hilversum",
      teeName: null,
      totalStrokes: 88,
      totalPutts: 33,
      penalties: null,
      fairwaysHit: null, // not shown in Balanced, so not sent
      gir: null,
      threePutts: null,
      blowUpHoles: null,
      notes: "windy",
    });
  });
});

describe("steppers", () => {
  it("start strokes at the typical score, scaled to the holes, or 90", () => {
    expect(step("totalStrokes", round({}), 1, 92)).toBe("92");
    expect(step("totalStrokes", round({ holes: 9 }), 1, 92)).toBe("46");
    expect(step("totalStrokes", round({}), -1, null)).toBe("90");
  });

  it("start putts at two a hole and the rest at zero", () => {
    expect(step("totalPutts", round({}), 1, null)).toBe("36");
    expect(step("penalties", round({}), 1, null)).toBe("0");
  });

  it("step by one, within the limits", () => {
    expect(step("totalStrokes", round({ totalStrokes: "88" }), 1, null)).toBe("89");
    expect(step("totalStrokes", round({ totalStrokes: "1" }), -1, null)).toBe("1");
    expect(step("penalties", round({ penalties: "0" }), -1, null)).toBe("0");
    expect(step("gir", round({ holes: 9, gir: "9" }), 1, null)).toBe("9");
  });

  it("show the cap that applies", () => {
    expect(maxFor("fairwaysHit", round({ holes: 9 }))).toBe(9);
    expect(maxFor("totalPutts", round({ totalStrokes: "88" }))).toBe(88);
    expect(maxFor("totalPutts", round({ totalStrokes: "" }))).toBeUndefined();
  });
});

describe("helpers", () => {
  it("parse whole numbers only", () => {
    expect(parseWhole(" 12 ")).toBe(12);
    expect(parseWhole("-3")).toBe(-3);
    for (const text of ["", "1.5", "abc", "1e2", "12a"]) expect(parseWhole(text)).toBeUndefined();
  });

  it("use the local calendar day for today", () => {
    expect(localIsoDate(new Date(2026, 0, 9, 23, 30))).toBe("2026-01-09");
  });
});
