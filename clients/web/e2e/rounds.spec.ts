import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * The Rounds section (#95) in a real browser, against a mocked API: the list beside the
 * round on desktop, deep links, and deleting through the confirmation dialog.
 */
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";
const ids = ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"];

async function mockApi(page: Page) {
  const rounds = ids.map((roundId, i) => ({
    roundId,
    userId,
    playedAt: i === 0 ? "2026-05-10" : "2026-05-03",
    holes: 18,
    courseName: i === 0 ? "Hilversum" : "De Pan",
    teeName: null,
    totalStrokes: 88 + i,
    totalPutts: 33,
    penalties: 1,
    fairwaysHit: null,
    gir: null,
    threePutts: null,
    blowUpHoles: 0,
    source: "Manual",
    notes: null,
    createdAt: "",
    updatedAt: "",
    fitnessScore: 72 - i,
    fitnessConfidence: "Medium",
  }));

  const json = (route: Route, status: number, body: unknown, headers: Record<string, string> = {}) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      headers,
      body: JSON.stringify(body),
    });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    const [, , id, sub] = path.split("/");
    const round = rounds.find((r) => r.roundId === id);

    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "0.0.0" } });
    if (path === "/auth/refresh")
      return json(route, 200, {
        userId,
        token: "t",
        refreshToken: null,
        expiresAt: new Date(Date.now() + 9e5).toISOString(),
      });
    if (path === "/me")
      return json(route, 200, {
        userId,
        email: "e@example.test",
        displayName: "E",
        timezone: null,
        units: "Metric",
        trackingMode: "Balanced",
        language: null,
        handicapIndex: null,
        typical18Score: null,
        goalFocus: [],
        createdAt: "",
        updatedAt: "",
      });
    if (path === "/me/rounds") return json(route, 200, rounds, { "X-Total-Count": String(rounds.length) });
    if (path.startsWith("/rounds/") && !round) return json(route, 404, { status: 404 });
    if (path.startsWith("/rounds/") && request.method() === "DELETE") {
      rounds.splice(rounds.indexOf(round!), 1);
      return route.fulfill({ status: 204 });
    }
    if (path.startsWith("/rounds/") && sub === undefined) return json(route, 200, round);
    if (sub === "fitness-score")
      return json(route, 200, {
        fitnessScoreId: "s",
        userId,
        roundId: id,
        scoreTotal: round!.fitnessScore,
        execution: 18,
        damageControl: 21,
        consistency: 23,
        habits: 10,
        confidenceLevel: "Medium",
        computedAt: "",
        explanation: {
          baselineAvgScore: 90,
          baselineStdDev: 3,
          roundsInHabitWindow: 2,
          practiceSessionsInHabitWindow: 0,
          reflected: false,
        },
      });
    if (sub === "micro-wins" || sub === "reflections") return json(route, 200, []);
    return json(route, 404, { status: 404 });
  });
}

test("on desktop, the list sits beside the round, and each round has its own URL", async ({ page }) => {
  await mockApi(page);
  await page.goto("/rounds");

  await expect(page.getByText("Choose a round to see its score.")).toBeVisible();
  await page.getByRole("link", { name: /De Pan/ }).click();

  await expect(page).toHaveURL(new RegExp(`/rounds/${ids[1]}$`));
  await expect(page.getByRole("heading", { level: 2, name: "May 3, 2026" })).toBeVisible();
  // The list is still there, with this round marked.
  await expect(page.getByRole("link", { name: /De Pan/ })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("link", { name: /Hilversum/ })).toBeVisible();

  // A deep link opens the same view.
  await page.goto(`/rounds/${ids[0]}`);
  await expect(page.getByRole("heading", { level: 2, name: "May 10, 2026" })).toBeVisible();
  await expect(page.getByRole("link", { name: /Hilversum/ })).toHaveAttribute("aria-current", "page");
});

test("deleting asks first, then the round leaves the list", async ({ page }) => {
  await mockApi(page);
  await page.goto(`/rounds/${ids[0]}/edit`);

  await page.getByRole("button", { name: "Delete round" }).click();
  const dialog = page.getByRole("dialog", { name: "Delete this round?" });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Cancel" })).toBeFocused();

  // Escape backs out, with focus back on Delete round.
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("button", { name: "Delete round" })).toBeFocused();

  await page.getByRole("button", { name: "Delete round" }).click();
  await dialog.getByRole("button", { name: "Delete" }).click();

  await expect(page).toHaveURL(/\/rounds$/);
  await expect(page.getByRole("link", { name: /Hilversum/ })).toHaveCount(0);
  await expect(page.getByRole("link", { name: /De Pan/ })).toBeVisible();
});

test("on a phone, the list and the round are separate pages", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockApi(page);
  await page.goto("/rounds");

  await page.getByRole("link", { name: /Hilversum/ }).click();

  await expect(page.getByRole("heading", { level: 1, name: "May 10, 2026" })).toBeVisible();
  await expect(page.getByRole("link", { name: /De Pan/ })).toHaveCount(0);
  await page.getByRole("link", { name: "Back to rounds" }).click();
  await expect(page.getByRole("link", { name: /De Pan/ })).toBeVisible();
});
