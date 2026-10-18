import { expect, test } from "@playwright/test";

/**
 * The app language in a real browser: without a choice in Profile it follows the
 * browser's language, before anyone has signed in, and asks the API for the same.
 */
test.use({ locale: "nl-NL" });

test("a Dutch browser gets the app in Dutch, and asks the API for Dutch", async ({ page }) => {
  const acceptLanguages: (string | undefined)[] = [];
  await page.route("**/api/**", async (route) => {
    acceptLanguages.push(route.request().headers()["accept-language"]);
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, "");
    if (path === "/client-config")
      return route.fulfill({ json: { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "0.0.0" } } });
    return route.fulfill({ status: 401, contentType: "application/problem+json", body: '{"status":401}' });
  });

  await page.goto("/");

  await expect(page.getByRole("link", { name: "Account aanmaken" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "nl");

  await page.getByRole("link", { name: "Inloggen" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Inloggen" })).toBeVisible();
  await expect(page.getByLabel("Wachtwoord", { exact: true })).toBeVisible();

  expect(acceptLanguages.length).toBeGreaterThan(0);
  expect(acceptLanguages.every((value) => value === "nl")).toBe(true);
});
