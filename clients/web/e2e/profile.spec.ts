import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * Profile (#111) in a real browser, against a mocked API: changing preferences, and
 * deleting the account through the modal dialog, which ends on Welcome.
 */
const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

async function mockApi(page: Page) {
  let me: Record<string, unknown> = {
    userId,
    email: "erwin@example.test",
    displayName: "Erwin",
    timezone: "Europe/Amsterdam",
    units: "Metric",
    trackingMode: "Minimal",
    handicapIndex: 18.4,
    typical18Score: 92,
    goalFocus: ["Putting"],
    createdAt: "2026-05-01T10:00:00Z",
    updatedAt: "2026-05-01T10:00:00Z",
  };
  let signedIn = true;

  const json = (route: Route, status: number, body: unknown) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      body: JSON.stringify(body),
    });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    const key = `${request.method()} ${path}`;

    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } });
    if (path === "/auth/refresh")
      return signedIn
        ? json(route, 200, {
            userId,
            token: "t",
            refreshToken: null,
            expiresAt: new Date(Date.now() + 9e5).toISOString(),
          })
        : json(route, 401, { status: 401 });
    if (key === "POST /auth/logout") {
      signedIn = false;
      return route.fulfill({ status: 204 });
    }
    if (key === "GET /me") return json(route, 200, me);
    if (key === "PATCH /me") {
      me = { ...me, ...(request.postDataJSON() as object) };
      return json(route, 200, me);
    }
    if (key === "DELETE /me") {
      const { password } = request.postDataJSON() as { password: string };
      return password === "correct horse"
        ? route.fulfill({ status: 204 })
        : json(route, 400, { status: 400, errors: { Password: ["The password is incorrect."] } });
    }
    return json(route, 404, { status: 404 });
  });
}

test("change preferences, then delete the account", async ({ page }) => {
  await mockApi(page);
  await page.goto("/profile");

  const preferences = page.getByRole("region", { name: "Preferences" });
  await preferences.getByRole("radio", { name: /Balanced/ }).check();
  await preferences.getByRole("checkbox", { name: "Penalties" }).check();
  await preferences.getByRole("button", { name: "Save" }).click();
  await expect(preferences.getByText("Changes saved.", { exact: true }).last()).toBeVisible();
  await page.screenshot({ path: "test-results/profile.png", fullPage: true });

  await page.getByRole("button", { name: "Delete account" }).click();
  const dialog = page.getByRole("dialog", { name: "Delete your account?" });
  await expect(dialog.getByLabel("Password")).toBeFocused();
  await dialog.getByLabel("Password").fill("wrong");
  await dialog.getByRole("button", { name: "Delete my account" }).click();
  await expect(dialog.getByRole("alert")).toHaveText("The password is incorrect.");
  await page.screenshot({ path: "test-results/profile-delete.png" });

  await dialog.getByLabel("Password").fill("correct horse");
  await dialog.getByRole("button", { name: "Delete my account" }).click();
  await expect(page).toHaveURL(/\/welcome$/);
  await expect(page.getByRole("heading", { level: 1, name: "GolfProgress" })).toBeVisible();
});
