import { describe, expect, expectTypeOf, it } from "vitest";
import spec from "../../../../api/openapi.json";
import { createApiClient, unwrap, type Schemas } from "./index";
import {
  knownMicroWinCategories,
  knownRoundSources,
  knownSessionTypes,
  microWinCategoryOf,
  roundSourceOf,
  sessionTypeOf,
} from "./knownValues";

/** Regression guard for #134: open enums never break this build when the API adds a value. */
describe("open enums", () => {
  it("handles a value added to the API after this build, without failing", async () => {
    const session: Schemas["PracticeSessionResponse"] = {
      practiceSessionId: "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a",
      userId: "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
      practicedAt: "2026-05-01",
      durationMinutes: 45,
      sessionType: "Fitness", // not a value this build knows
      venueName: null,
      tags: [],
      primaryGoal: null,
      notes: null,
      createdAt: "2026-05-01T09:30:00+00:00",
    };
    const api = createApiClient({
      baseUrl: "https://api.example.test",
      tokens: { accessToken: () => "t", refreshAccessToken: async () => null },
      fetch: async () => new Response(JSON.stringify(session), { headers: { "Content-Type": "application/json" } }),
    });

    const result = unwrap(
      await api.GET("/me/practice-sessions/{id}", { params: { path: { id: session.practiceSessionId } } }),
    );

    expect(result.sessionType).toBe("Fitness");
    expect(sessionTypeOf(result.sessionType)).toBe("unknown");
  });

  it("maps the values it knows", () => {
    expect(sessionTypeOf("Putting")).toBe("Putting");
    expect(microWinCategoryOf("Habit")).toBe("Habit");
    expect(roundSourceOf("Import")).toBe("Import");
    expect(roundSourceOf(null)).toBe("unknown");
  });

  it("types open fields as plain strings and closed ones as unions", () => {
    expectTypeOf<Schemas["PracticeSessionResponse"]["sessionType"]>().toEqualTypeOf<string>();
    expectTypeOf<Schemas["CreatePracticeSessionRequest"]["sessionType"]>().not.toEqualTypeOf<string>();
    expectTypeOf(sessionTypeOf("x")).toEqualTypeOf<
      "Range" | "Putting" | "ShortGame" | "Simulator" | "Lesson" | "Other" | "unknown"
    >();
  });

  /** So the build knows every value the API already returns. */
  it.each([
    ["PracticeSessionResponse", "sessionType", knownSessionTypes],
    ["MicroWinResponse", "category", knownMicroWinCategories],
    ["RoundResponse", "source", knownRoundSources],
  ] as const)("lists the same known values as the spec for %s.%s", (schema, property, known) => {
    const schemas = spec.components.schemas as Record<string, { properties: Record<string, Record<string, unknown>> }>;

    expect(schemas[schema]?.properties[property]?.["x-known-values"]).toEqual([...known]);
  });
});
