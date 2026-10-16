import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * Practice (#103) in a real browser, against a mocked API: logging a session with tag
 * chips, seeing it in the list, and deleting it through the confirmation dialog.
 */
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";
const newId = "99999999-9999-9999-9999-999999999999";

async function mockApi(page: Page) {
  const sessions: Record<string, unknown>[] = [];

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
    const key = `${request.method()} ${path}`;

    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } });
    if (path === "/auth/refresh")
      return json(route, 200, {
        userId,
        token: "t",
        refreshToken: null,
        expiresAt: new Date(Date.now() + 9e5).toISOString(),
      });
    if (key === "GET /me/practice-sessions")
      return json(route, 200, sessions, { "X-Total-Count": String(sessions.length) });
    if (key === "POST /me/practice-sessions") {
      const saved = { ...(request.postDataJSON() as object), practiceSessionId: newId, userId, createdAt: "" };
      sessions.unshift(saved);
      return json(route, 201, saved);
    }
    if (key === `GET /me/practice-sessions/${newId}`)
      return sessions.length ? json(route, 200, sessions[0]) : json(route, 404, { status: 404 });
    if (key === `DELETE /me/practice-sessions/${newId}`) {
      sessions.length = 0;
      return route.fulfill({ status: 204 });
    }
    return json(route, 404, { status: 404 });
  });
}

test("log a practice session with tags, then delete it", async ({ page }) => {
  await mockApi(page);
  await page.goto("/practice");

  await expect(page.getByText("No practice logged yet.", { exact: false })).toBeVisible();
  await page.getByRole("link", { name: "Log practice" }).click();

  await page.getByLabel("Type").selectOption({ label: "Putting" });
  await page.getByLabel("Duration").fill("45");
  await page.getByLabel("Tags").fill("lag putting");
  await page.getByLabel("Tags").press("Enter");
  await page.getByLabel("Tags").fill("speed control");
  await page.getByLabel("Tags").press(",");
  await expect(page.getByRole("button", { name: "Remove tag speed control" })).toBeVisible();
  await page.getByRole("button", { name: "Save" }).click();

  await expect(page).toHaveURL(new RegExp(`/practice/${newId}$`));
  await expect(page.getByRole("heading", { level: 1, name: "Putting" })).toBeVisible();
  await expect(page.getByText("45 min")).toBeVisible();

  await page.getByRole("link", { name: "Back to practice" }).click();
  await expect(page.getByRole("link", { name: /Putting/ })).toBeVisible();

  await page.getByRole("link", { name: /Putting/ }).click();
  await page.getByRole("button", { name: "Delete session" }).click();
  const dialog = page.getByRole("dialog", { name: "Delete this practice session?" });
  await expect(dialog.getByRole("button", { name: "Cancel" })).toBeFocused();
  await dialog.getByRole("button", { name: "Delete" }).click();

  await expect(page).toHaveURL(/\/practice$/);
  await expect(page.getByText("No practice logged yet.", { exact: false })).toBeVisible();
});
