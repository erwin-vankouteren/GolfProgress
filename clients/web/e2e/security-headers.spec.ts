import { readFileSync } from "node:fs";
import { expect, test, type Page, type Route } from "@playwright/test";

/**
 * The staging headers (#116): public/staticwebapp.config.json's Content Security Policy
 * and friends, applied to the production build here as Azure Static Web Apps applies
 * them there. The app must work under them, with no CSP violation on any screen, and
 * deep links must reach the app (the navigation fallback).
 */
const config = JSON.parse(readFileSync(new URL("../public/staticwebapp.config.json", import.meta.url), "utf8")) as {
  globalHeaders: Record<string, string>;
  navigationFallback: { rewrite: string };
};

const userId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

/** The deploy workflow puts the API's origin in; the e2e API is same-origin. */
const headers = Object.fromEntries(
  Object.entries(config.globalHeaders).map(([name, value]) => [name, value.replace("__API_ORIGIN__", "")]),
);

async function serveWithHeaders(page: Page) {
  const violations: string[] = [];
  page.on("console", (message) => {
    if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text());
  });
  page.on("pageerror", (error) => violations.push(error.message));

  // The page itself gets the headers, as Static Web Apps sends them.
  await page.route(
    (url) => !url.pathname.startsWith("/api/") && !url.pathname.startsWith("/assets/"),
    async (route) => {
      if (route.request().resourceType() !== "document") return route.continue();
      const response = await route.fetch();
      await route.fulfill({ response, headers: { ...response.headers(), ...headers } });
    },
  );

  const json = (route: Route, status: number, body: unknown, extra: Record<string, string> = {}) =>
    route.fulfill({
      status,
      contentType: status >= 400 ? "application/problem+json" : "application/json",
      headers: extra,
      body: JSON.stringify(body),
    });
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname.replace(/^\/api/, "");
    if (path === "/client-config")
      return json(route, 200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } });
    if (path === "/auth/refresh")
      return json(route, 200, {
        userId,
        token: "t",
        refreshToken: null,
        expiresAt: new Date(Date.now() + 9e5).toISOString(),
      });
    if (path === "/me/rounds") return json(route, 200, [], { "X-Total-Count": "3" });
    if (path === "/me/timeline")
      return json(route, 200, {
        userId,
        count: 3,
        items: [1, 2, 3].map((i) => ({
          roundId: `00000000-0000-0000-0000-00000000000${i}`,
          playedAt: `2026-10-0${i}`,
          holes: 18,
          totalStrokes: 90 - i,
          totalPutts: null,
          penalties: null,
          threePutts: null,
          blowUpHoles: null,
          fitnessScoreTotal: 60 + i,
          fitnessConfidence: "Medium",
          microWinTitle: null,
        })),
      });
    if (path === "/me/timeline/monthly") return json(route, 200, { userId, months: 0, items: [] });
    return json(route, 404, { status: 404 });
  });

  return violations;
}

test("the app works under the staging Content Security Policy", async ({ page }) => {
  const violations = await serveWithHeaders(page);

  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1, name: "Home" })).toBeVisible();

  // The charts are the likeliest to need something the policy forbids.
  await page.goto("/progress");
  await expect(page.getByRole("heading", { name: "Rounds over time" })).toBeVisible();
  await expect(page.locator(".recharts-line")).not.toHaveCount(0);

  expect(violations).toEqual([]);
});

test("the security headers are all there, and deep links fall back to the app", () => {
  expect(config.navigationFallback.rewrite).toBe("/index.html");
  expect(Object.keys(config.globalHeaders).sort()).toEqual([
    "Content-Security-Policy",
    "Permissions-Policy",
    "Referrer-Policy",
    "Strict-Transport-Security",
    "X-Content-Type-Options",
  ]);
  expect(config.globalHeaders["Content-Security-Policy"]).toContain("connect-src 'self' __API_ORIGIN__");
  expect(config.globalHeaders["Content-Security-Policy"]).toContain("frame-ancestors 'none'");
});

test("the control: the policy really is applied, and blocks another origin", async ({ page }) => {
  const violations = await serveWithHeaders(page);
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1, name: "Home" })).toBeVisible();

  await page.evaluate(() => fetch("https://example.org/").catch(() => undefined));

  await expect.poll(() => violations.some((v) => v.includes("example.org"))).toBe(true);
});
