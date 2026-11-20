import { describe, expect, it } from "vitest";
import { queryKeys } from "./queries";
import { scopedKey } from "./studentScope";

describe("scopedKey (#206)", () => {
  it("leaves the golfer's own keys as they are", () => {
    expect(scopedKey(undefined, queryKeys.round("r"))).toEqual(["rounds", "r"]);
  });

  it("puts a student's under students, so they never share a cache entry with the golfer's", () => {
    expect(scopedKey("g", queryKeys.round("r"))).toEqual(["students", "g", "rounds", "r"]);
    expect(scopedKey("g", queryKeys.practiceList).slice(0, 1)).toEqual(queryKeys.students);
  });
});
