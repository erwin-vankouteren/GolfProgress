import { describe, expect, it } from "vitest";
import { emptyReflection, hasContent, tooLong, toReflectionRequest } from "./reflectionForm";

describe("reflection rules (#99)", () => {
  it("need at least one thing filled in; spaces alone don't count", () => {
    expect(hasContent(emptyReflection())).toBe(false);
    expect(hasContent({ ...emptyReflection(), whatWorked: "   " })).toBe(false);
    expect(hasContent({ ...emptyReflection(), focusRating: 4 })).toBe(true);
    // "No" is an answer.
    expect(hasContent({ ...emptyReflection(), frustrationSpillover: false })).toBe(true);
    expect(hasContent({ ...emptyReflection(), oneChangeNext: "Slow down" })).toBe(true);
  });

  it("bound each text to 1,000 characters, counted as people see them", () => {
    expect(tooLong({ ...emptyReflection(), whatWorked: "x".repeat(1000) })).toEqual([]);
    expect(tooLong({ ...emptyReflection(), whatWorked: "x".repeat(1001) })).toEqual(["whatWorked"]);
    // 1,000 emoji are 2,000 UTF-16 units, but 1,000 characters.
    expect(tooLong({ ...emptyReflection(), whatToRepeat: "⛳".repeat(1000) })).toEqual([]);
  });

  it("send trimmed text, and nothing for what was left empty", () => {
    expect(
      toReflectionRequest({ ...emptyReflection(), moodRating: 3, whatWorked: "  Patience  ", whatToRepeat: " " }),
    ).toEqual({
      focusRating: null,
      moodRating: 3,
      frustrationSpillover: null,
      whatWorked: "Patience",
      whatToRepeat: null,
      oneChangeNext: null,
    });
  });
});
