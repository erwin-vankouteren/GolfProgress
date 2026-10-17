import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * Registering, signing in, staying signed in across a reload, and signing out (#69),
 * against a mocked API. The mock stands in for the HttpOnly refresh cookie with
 * `cookieValid`: set by register/login, cleared by logout.
 */
async function mockApi(page: Page) {
  const api = { cookieValid: false, registered: new Map<string, string>() };

  const json = (route: Route, status: number, body: unknown) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      body: JSON.stringify(body),
    });

  const signedIn = () => ({
    userId: "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f",
    token: `access-${Date.now()}`,
    refreshToken: null,
    expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    const body = request.postDataJSON() as Record<string, string> | null;

    switch (`${request.method()} ${path}`) {
      case "GET /client-config":
        return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "0.0.0" } });
      case "POST /auth/register":
        if (api.registered.has(body!.email!)) return json(route, 409, { status: 409, title: "Conflict" });
        api.registered.set(body!.email!, body!.password!);
        api.cookieValid = true;
        return json(route, 200, signedIn());
      case "POST /auth/login":
        if (api.registered.get(body!.email!) !== body!.password) {
          return json(route, 401, { status: 401, title: "Unauthorized" });
        }
        api.cookieValid = true;
        return json(route, 200, signedIn());
      case "POST /auth/refresh":
        return api.cookieValid ? json(route, 200, signedIn()) : json(route, 401, { status: 401 });
      case "POST /auth/logout":
        api.cookieValid = false;
        return route.fulfill({ status: 204 });
      default:
        return json(route, 404, { status: 404 });
    }
  });

  return api;
}

async function expectNoStoredTokens(page: Page) {
  const stored = await page.evaluate(() => [localStorage.length, sessionStorage.length]);
  expect(stored).toEqual([0, 0]);
}

test("register, stay signed in across a reload, sign out and sign in again", async ({ page }) => {
  await mockApi(page);

  await page.goto("/");
  await expect(page).toHaveURL(/\/welcome$/);

  await page.getByRole("link", { name: "Create account" }).click();
  await page.getByLabel("Name").fill("Erwin");
  await page.getByLabel("Email").fill("erwin@example.test");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Create account" }).click();

  // A new account starts onboarding (#111); skipping it leads Home.
  await expect(page.getByRole("heading", { level: 1, name: "Which units do you use?" })).toBeVisible();
  await page.getByRole("button", { name: "Skip for now" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Home" })).toBeVisible();
  await expectNoStoredTokens(page);

  // The access token was in memory only; the "cookie" restores the session.
  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: "Home" })).toBeVisible();
  await expectNoStoredTokens(page);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profile" }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/welcome$/);

  // Signed out stays signed out after a reload.
  await page.reload();
  await expect(page).toHaveURL(/\/welcome$/);

  await page.getByRole("link", { name: "Sign in" }).click();
  await page.getByLabel("Email").fill("erwin@example.test");
  await page.getByLabel("Password").fill("wrong");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("alert")).toHaveText("Email or password is wrong.");

  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Home" })).toBeVisible();
  await expectNoStoredTokens(page);
});

test("returns to the page the user was heading for", async ({ page }) => {
  const api = await mockApi(page);
  api.registered.set("erwin@example.test", "correct horse battery");

  await page.goto("/progress");
  await expect(page).toHaveURL(/\/welcome$/);

  await page.getByRole("link", { name: "Sign in" }).click();
  await page.getByLabel("Email").fill("erwin@example.test");
  await page.getByLabel("Password").fill("correct horse battery");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/progress$/);
  await expect(page.getByRole("heading", { level: 1, name: "Progress" })).toBeVisible();
});
