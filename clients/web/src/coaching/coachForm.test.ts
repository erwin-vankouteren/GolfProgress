import { describe, expect, it } from "vitest";
import { ApiProblem } from "../api";
import { checkCoachEmail, invitationFailure, sharedItems } from "./coachForm";

describe("checkCoachEmail", () => {
  it("needs an address of the right shape", () => {
    expect(checkCoachEmail("  ")).toBe("required");
    expect(checkCoachEmail("coach@")).toBe("invalid");
    expect(checkCoachEmail(" coach@example.test ")).toBeUndefined();
  });
});

describe("sharedItems", () => {
  it("always includes rounds and progress", () => {
    expect(sharedItems({ shareReflections: false, sharePractice: false })).toEqual(["rounds"]);
    expect(sharedItems({ shareReflections: true, sharePractice: false })).toEqual(["rounds", "reflections"]);
    expect(sharedItems({ shareReflections: true, sharePractice: true })).toEqual(["rounds", "reflections", "practice"]);
  });
});

describe("invitationFailure", () => {
  it("tells the API's answers apart", () => {
    expect(invitationFailure(new ApiProblem(404))).toBe("gone");
    expect(invitationFailure(new ApiProblem(400))).toBe("gone");
    expect(invitationFailure(new ApiProblem(403))).toBe("wrongAccount");
    expect(invitationFailure(new ApiProblem(409))).toBe("full");
    expect(invitationFailure(new ApiProblem(500))).toBe("other");
    expect(invitationFailure(new TypeError("Failed to fetch"))).toBe("connection");
  });
});
