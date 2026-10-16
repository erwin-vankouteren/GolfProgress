import { describe, expect, it } from "vitest";
import { addTag, emptyPractice, toPracticeRequest, validatePractice, type PracticeFormValues } from "./practiceForm";

const practice = (values: Partial<PracticeFormValues>): PracticeFormValues => ({
  ...emptyPractice(new Date(2026, 4, 12)),
  ...values,
});

describe("practice rules (#103), as the API's", () => {
  it("accept a session with just a date and type", () => {
    expect(validatePractice(practice({}))).toEqual({});
  });

  it("need a date", () => {
    expect(validatePractice(practice({ practicedAt: "" })).practicedAt?.code).toBe("dateRequired");
  });

  it("take a duration of 1 to 1,440 whole minutes, or none", () => {
    expect(validatePractice(practice({ durationMinutes: "" }))).toEqual({});
    expect(validatePractice(practice({ durationMinutes: "1440" }))).toEqual({});
    expect(validatePractice(practice({ durationMinutes: "0" })).durationMinutes?.code).toBe("durationRange");
    expect(validatePractice(practice({ durationMinutes: "1441" })).durationMinutes?.code).toBe("durationRange");
    expect(validatePractice(practice({ durationMinutes: "45.5" })).durationMinutes?.code).toBe("wholeNumber");
  });

  it("bound the text fields", () => {
    expect(validatePractice(practice({ venueName: "x".repeat(201) })).venueName).toEqual({ code: "tooLong", max: 200 });
    expect(validatePractice(practice({ notes: "x".repeat(2001) })).notes).toEqual({ code: "tooLong", max: 2000 });
  });
});

describe("tags", () => {
  it("are trimmed and added", () => {
    expect(addTag(["range"], "  lag putting ")).toEqual({ ok: true, tags: ["range", "lag putting"] });
  });

  it("refuse empty, duplicate (in any case), over 50 characters, and a 21st", () => {
    expect(addTag([], "   ")).toEqual({ ok: false, reason: "empty" });
    expect(addTag(["Lag putting"], "lag PUTTING")).toEqual({ ok: false, reason: "duplicate" });
    expect(addTag([], "x".repeat(51))).toEqual({ ok: false, reason: "tooLong" });
    const twenty = Array.from({ length: 20 }, (_, i) => `t${i}`);
    expect(addTag(twenty, "one more")).toEqual({ ok: false, reason: "tooMany" });
  });
});

describe("the request", () => {
  it("sends numbers and tags, and empty text as null", () => {
    expect(
      toPracticeRequest(
        practice({
          sessionType: "Putting",
          durationMinutes: "45",
          venueName: " ",
          tags: ["lag putting"],
          notes: " ok ",
        }),
      ),
    ).toEqual({
      practicedAt: "2026-05-12",
      sessionType: "Putting",
      durationMinutes: 45,
      venueName: null,
      primaryGoal: null,
      tags: ["lag putting"],
      notes: "ok",
    });
  });
});
