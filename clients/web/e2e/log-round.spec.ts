import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * Logging a round (#92) in a real browser, against a mocked API: the dialog on desktop,
 * which jsdom can't run, and the full page on a phone.
 */
const roundId = "6a1f0c2e-1b2d-4c3e-8f9a-0b1c2d3e4f5a";
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

async function mockApi(page: Page) {
  let saved: Record<string, unknown> | null = null;

  const json = (route: Route, status: number, body: unknown) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      body: JSON.stringify(body),
    });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");

    switch (`${request.method()} ${path}`) {
      case "GET /client-config":
        return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "0.0.0" } });
      // Signed in already: the refresh cookie restores the session.
      case "POST /auth/refresh":
        return json(route, 200, {
          userId,
          token: "access",
          refreshToken: null,
          expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        });
      case "GET /me":
        return json(route, 200, {
          userId,
          email: "erwin@example.test",
          displayName: "Erwin",
          timezone: null,
          units: "Metric",
          trackingMode: "Balanced",
          handicapIndex: null,
          typical18Score: 92,
          goalFocus: [],
          createdAt: "2026-05-01T00:00:00Z",
          updatedAt: "2026-05-01T00:00:00Z",
        });
      case "POST /me/rounds": {
        const body = request.postDataJSON() as Record<string, unknown>;
        saved = { ...body, roundId, userId, source: "Manual", createdAt: "", updatedAt: "" };
        return json(route, 201, saved);
      }
      case `GET /rounds/${roundId}`:
        return json(route, 200, saved);
      case `GET /rounds/${roundId}/fitness-score`:
        return json(route, 200, {
          fitnessScoreId: "s",
          userId,
          roundId,
          scoreTotal: 72,
          execution: 18,
          damageControl: 21,
          consistency: 23,
          habits: 10,
          confidenceLevel: "Medium",
          computedAt: "",
        });
      case `GET /rounds/${roundId}/micro-wins`:
        return json(route, 200, []);
      default:
        return json(route, 404, { status: 404 });
    }
  });
}

test("on desktop, Log round opens a dialog, and Save opens the scored round", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");

  const logRound = page.getByRole("banner").getByRole("link", { name: "Log round" });
  await logRound.click();

  const dialog = page.getByRole("dialog", { name: "Log round" });
  await expect(dialog).toBeVisible();
  // Still on the page it was opened from.
  await expect(page).toHaveURL(/\/$/);

  await dialog.getByLabel("Strokes", { exact: true }).fill("88");
  await dialog.getByLabel("Putts", { exact: true }).fill("33");
  await dialog.getByLabel("Putts", { exact: true }).press("Enter");

  await expect(page).toHaveURL(new RegExp(`/rounds/${roundId}$`));
  await expect(dialog).toBeHidden();
  await expect(page.getByText("72", { exact: true })).toBeVisible();
  await expect(page.getByText("Medium")).toBeVisible();
});

test("on desktop, Escape closes the dialog and returns focus to Log round", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");

  const logRound = page.getByRole("banner").getByRole("link", { name: "Log round" });
  await logRound.click();
  await expect(page.getByRole("dialog", { name: "Log round" })).toBeVisible();

  await page.keyboard.press("Escape");

  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(logRound).toBeFocused();
});

test("on a phone, Log round is a page of its own", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockApi(page);
  await page.goto("/");

  await page.getByRole("banner").getByRole("link", { name: "Log round" }).click();

  await expect(page).toHaveURL(/\/rounds\/new$/);
  await expect(page.getByRole("heading", { level: 1, name: "Log round" })).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);

  await page.getByLabel("Strokes", { exact: true }).fill("88");
  await page.getByRole("button", { name: "Save" }).click();

  await expect(page).toHaveURL(new RegExp(`/rounds/${roundId}$`));
});
