import { describe, expect, it } from "vitest";
import {
  checkNewPassword,
  parseDecimal,
  preferencesOf,
  toProfileRequest,
  toggleGoalFocus,
  validateProfile,
  type Me,
} from "./profileForm";

const values = (overrides: Partial<Record<"displayName" | "handicapIndex" | "typical18Score", string>> = {}) => ({
  displayName: "Erwin",
  handicapIndex: "",
  typical18Score: "",
  ...overrides,
});

describe("profile (#111)", () => {
  it("needs a name, at most 100 characters as people count them", () => {
    expect(validateProfile(values({ displayName: "  " })).displayName).toEqual({ kind: "required" });
    expect(validateProfile(values({ displayName: "x".repeat(101) })).displayName).toEqual({
      kind: "tooLong",
      max: 100,
    });
    expect(validateProfile(values({ displayName: "🏌".repeat(100) })).displayName).toBeUndefined();
  });

  it("takes a handicap from -10 to 54, with a point or a comma", () => {
    expect(parseDecimal("18,4")).toBe(18.4);
    expect(parseDecimal("+2.1")).toBe(2.1);
    expect(parseDecimal("abc")).toBeUndefined();
    expect(validateProfile(values({ handicapIndex: "54.1" })).handicapIndex).toEqual({
      kind: "outOfRange",
      min: -10,
      max: 54,
    });
    expect(validateProfile(values({ handicapIndex: "-10" })).handicapIndex).toBeUndefined();
    expect(validateProfile(values({ handicapIndex: "x" })).handicapIndex).toEqual({ kind: "notANumber" });
  });

  it("takes a whole typical score from 18 to 200", () => {
    expect(validateProfile(values({ typical18Score: "92.5" })).typical18Score).toEqual({ kind: "notWhole" });
    expect(validateProfile(values({ typical18Score: "17" })).typical18Score).toEqual({
      kind: "outOfRange",
      min: 18,
      max: 200,
    });
    expect(validateProfile(values({ typical18Score: "92" }))).toEqual({});
  });

  it("clears an emptied handicap or typical score", () => {
    expect(toProfileRequest(values({ displayName: " Erwin ", handicapIndex: "18,4" }))).toEqual({
      displayName: "Erwin",
      handicapIndex: 18.4,
      typical18Score: null,
    });
  });
});

describe("preferences", () => {
  it("keeps at most three goal focus areas, in the API's order", () => {
    expect(toggleGoalFocus(["Consistency"], "Putting")).toEqual(["Putting", "Consistency"]);
    expect(toggleGoalFocus(["Putting", "Penalties", "TeeShots"], "Consistency")).toEqual([
      "Putting",
      "Penalties",
      "TeeShots",
    ]);
    expect(toggleGoalFocus(["Putting", "Penalties"], "Putting")).toEqual(["Penalties"]);
  });

  it("ignores goal focus areas this build doesn't know", () => {
    const me = { units: "Metric", trackingMode: "Balanced", goalFocus: ["Driving", "Putting"] } as unknown as Me;
    expect(preferencesOf(me).goalFocus).toEqual(["Putting"]);
  });
});

describe("new password", () => {
  it("follows the API's password policy", () => {
    expect(checkNewPassword("", "old", null)).toEqual({ kind: "required" });
    expect(checkNewPassword("short", "old", null)).toEqual({ kind: "tooShort", min: 8 });
    expect(checkNewPassword("x".repeat(129), "old", null)).toEqual({ kind: "tooLong", max: 128 });
    expect(checkNewPassword("        ", "old", null)).toEqual({ kind: "onlySpaces" });
    expect(checkNewPassword(" Me@Example.test ", "old", "me@example.test")).toEqual({ kind: "isEmail" });
    expect(checkNewPassword("same password", "same password", null)).toEqual({ kind: "unchanged" });
    // Eight emoji are eight characters, not sixteen units.
    expect(checkNewPassword("🏌".repeat(8), "old", null)).toBeUndefined();
  });
});
