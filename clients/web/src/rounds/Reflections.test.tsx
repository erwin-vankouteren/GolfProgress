import { screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { json, renderSignedIn, testUserId as userId, type Handler } from "../test/fakeApi";

const roundId = "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a";

/**
 * A round with no reflection yet, on a fake API that stores the reflections posted to it.
 * `post` replaces how POST answers, for error cases.
 */
function renderRound(post?: Handler) {
  const reflections: Record<string, unknown>[] = [];
  const posted: Record<string, unknown>[] = [];

  const result = renderSignedIn(`/rounds/${roundId}`, {
    [`GET /rounds/${roundId}`]: () =>
      json(200, {
        roundId,
        userId,
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
        fitnessScore: null,
        fitnessConfidence: null,
      }),
    [`GET /rounds/${roundId}/fitness-score`]: () => json(404, { status: 404 }),
    [`GET /rounds/${roundId}/micro-wins`]: () => json(200, []),
    [`GET /rounds/${roundId}/reflections`]: () => json(200, reflections),
    [`POST /rounds/${roundId}/reflections`]:
      post ??
      (async (request) => {
        const body = (await request.json()) as Record<string, unknown>;
        posted.push(body);
        const saved = {
          ...body,
          reflectionId: `r${reflections.length}`,
          userId,
          roundId,
          createdAt: "2026-05-03T18:00:00Z",
        };
        reflections.push(saved);
        return json(200, saved);
      }),
  });
  return { ...result, posted };
}

const reflectionCard = () => screen.getByRole("region", { name: "Reflection" });

afterEach(() => vi.unstubAllGlobals());

describe("reflections (#99)", () => {
  it("adds a reflection inline and shows it on the round", async () => {
    const { user, posted } = renderRound();

    expect(await screen.findByText(/No reflection yet/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Add reflection" }));

    // Explains what it's for, and opening the form takes focus there.
    expect(screen.getByText(/counts towards the Habits part of your fitness score/)).toBeInTheDocument();
    const focus = within(screen.getByRole("group", { name: "Focus" }));
    expect(focus.getByRole("radio", { name: "1 of 5" })).toHaveFocus();

    await user.click(focus.getByRole("radio", { name: "4 of 5" }));
    await user.click(within(screen.getByRole("group", { name: "Mood" })).getByRole("radio", { name: "3 of 5" }));
    await user.click(
      within(screen.getByRole("group", { name: "Did frustration carry over between holes?" })).getByRole("radio", {
        name: "No",
      }),
    );
    await user.type(screen.getByLabelText("What worked"), "  Kept the driver in play ");
    await user.click(screen.getByRole("button", { name: "Save reflection" }));

    expect(await screen.findByText("Reflection saved.")).toBeInTheDocument();
    expect(posted[0]).toEqual({
      focusRating: 4,
      moodRating: 3,
      frustrationSpillover: false,
      whatWorked: "Kept the driver in play",
      whatToRepeat: null,
      oneChangeNext: null,
    });

    const card = within(reflectionCard());
    expect(await card.findByText("4 of 5")).toBeInTheDocument();
    expect(card.getByText("Kept the driver in play")).toBeInTheDocument();
    expect(card.queryByText(/No reflection yet/)).not.toBeInTheDocument();
    // Focus goes back to where the user started.
    await waitFor(() => expect(card.getByRole("button", { name: "Add another reflection" })).toHaveFocus());
  });

  it("asks for at least one thing, without sending", async () => {
    const { user, posted } = renderRound();

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));
    await user.type(screen.getByLabelText("What to repeat"), "   ");
    await user.click(screen.getByRole("button", { name: "Save reflection" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Fill in at least one thing to save a reflection.");
    expect(posted).toHaveLength(0);
  });

  it("lets a rating be cleared", async () => {
    const { user } = renderRound();

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));
    const mood = within(screen.getByRole("group", { name: "Mood" }));
    await user.click(mood.getByRole("radio", { name: "2 of 5" }));
    await user.click(mood.getByRole("button", { name: "Clear Mood" }));

    expect(mood.getByRole("radio", { name: "2 of 5" })).not.toBeChecked();
    expect(mood.queryByRole("button", { name: "Clear Mood" })).not.toBeInTheDocument();
  });

  it("bounds the text, as the API does", async () => {
    const { user, posted } = renderRound();

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));
    const field = screen.getByLabelText("One change for next time");
    await user.click(field);
    await user.paste("x".repeat(1001));

    expect(screen.getByText("Use at most 1,000 characters.")).toBeInTheDocument();
    expect(field).toHaveAttribute("aria-invalid", "true");
    await user.click(screen.getByRole("button", { name: "Save reflection" }));
    expect(posted).toHaveLength(0);
  });

  it("shows the API's messages next to their fields", async () => {
    const { user } = renderRound(() =>
      json(400, { status: 400, errors: { WhatWorked: ["The field WhatWorked must be at most 1000 characters."] } }),
    );

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));
    await user.type(screen.getByLabelText("What worked"), "Patience");
    await user.click(screen.getByRole("button", { name: "Save reflection" }));

    expect(await screen.findByText("The field WhatWorked must be at most 1000 characters.")).toBeInTheDocument();
  });

  it("can be cancelled without saving", async () => {
    const { user, posted } = renderRound();

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));
    await user.type(screen.getByLabelText("What worked"), "Patience");
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("button", { name: "Save reflection" })).not.toBeInTheDocument();
    expect(posted).toHaveLength(0);
    expect(screen.getByText(/No reflection yet/)).toBeInTheDocument();
  });

  it("can't be saved offline", async () => {
    vi.stubGlobal("navigator", { ...navigator, onLine: false });
    const { user } = renderRound();

    await user.click(await screen.findByRole("button", { name: "Add reflection" }));

    expect(screen.getByRole("button", { name: "Save reflection" })).toBeDisabled();
    expect(screen.getByText("You're offline. Connect to save this reflection.")).toBeInTheDocument();
  });
});
