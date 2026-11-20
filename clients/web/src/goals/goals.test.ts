import { describe, expect, it } from "vitest";
import {
  emptyGoal,
  goalToForm,
  goalsToShow,
  linkableGoals,
  progressOf,
  suggestedGoal,
  toGoalRequest,
  validateGoal,
  type PracticeGoal,
} from "./goals";

const goal = (overrides: Partial<PracticeGoal> = {}): PracticeGoal => ({
  practiceGoalId: "g1",
  golferId: "ann",
  coachId: "pro",
  coachName: "Pro",
  title: "Lag putts",
  details: null,
  focusArea: "Putting",
  sessionType: "Putting",
  targetSessions: 6,
  linkedSessions: 2,
  startsOn: "2026-11-01",
  endsOn: "2026-11-28",
  status: "Active",
  createdAt: "",
  updatedAt: null,
  ...overrides,
});

describe("which goals (#230)", () => {
  it("shows active and ended goals on Practice, not done or dismissed ones", () => {
    const goals = ["Active", "Ended", "Done", "Dismissed"].map((status, i) =>
      goal({ practiceGoalId: `g${i}`, status: status as PracticeGoal["status"] }),
    );
    expect(goalsToShow(goals).map((g) => g.status)).toEqual(["Active", "Ended"]);
  });

  it("counts a session towards an active goal whose period includes its date", () => {
    const goals = [goal(), goal({ practiceGoalId: "ended", status: "Ended" })];
    expect(linkableGoals(goals, "2026-11-10").map((g) => g.practiceGoalId)).toEqual(["g1"]);
    expect(linkableGoals(goals, "2026-10-31")).toEqual([]);
    expect(linkableGoals(goals, "2026-11-28")).toHaveLength(1);
  });

  it("suggests the goal set for that type of session", () => {
    const goals = [goal({ practiceGoalId: "any", sessionType: null }), goal()];
    expect(suggestedGoal(goals, "2026-11-10", "Putting")?.practiceGoalId).toBe("g1");
    expect(suggestedGoal(goals, "2026-11-10", "Range")).toBeUndefined();
  });

  it("knows when the target is reached", () => {
    expect(progressOf(goal({ linkedSessions: 6 }))).toEqual({ linked: 6, target: 6, reached: true });
  });
});

describe("the coach's goal form (#227)", () => {
  const today = new Date(2026, 10, 20);

  it("starts today, for four weeks and six sessions", () => {
    expect(emptyGoal(today)).toMatchObject({ startsOn: "2026-11-20", endsOn: "2026-12-18", targetSessions: "6" });
  });

  it("checks what the API checks", () => {
    const base = emptyGoal(today);
    expect(validateGoal({ ...base, title: " " }, today).title).toEqual({ code: "required" });
    expect(validateGoal({ ...base, title: "t".repeat(101) }, today).title).toEqual({ code: "tooLong", max: 100 });
    expect(validateGoal({ ...base, title: "Putts", targetSessions: "51" }, today).targetSessions?.code).toBe(
      "targetRange",
    );
    expect(validateGoal({ ...base, title: "Putts", endsOn: "2027-01-16" }, today).endsOn).toEqual({
      code: "tooLongPeriod",
      weeks: 8,
    });
    expect(validateGoal({ ...base, title: "Putts", endsOn: "2026-11-19" }, today).endsOn?.code).toBe("endBeforeStart");
    expect(
      validateGoal({ ...base, title: "Putts", startsOn: "2026-11-01", endsOn: "2026-11-19" }, today).endsOn?.code,
    ).toBe("endInPast");
    expect(validateGoal({ ...base, title: "Putts" }, today)).toEqual({});
  });

  it("sends empty choices as null, and round-trips a goal", () => {
    expect(toGoalRequest({ ...emptyGoal(today), title: " Putts ", details: " " })).toMatchObject({
      title: "Putts",
      details: null,
      focusArea: null,
      sessionType: null,
      targetSessions: 6,
    });
    expect(toGoalRequest(goalToForm(goal()))).toMatchObject({ focusArea: "Putting", sessionType: "Putting" });
  });
});
