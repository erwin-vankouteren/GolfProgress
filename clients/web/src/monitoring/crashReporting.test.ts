import type { ErrorEvent } from "@sentry/react";
import { describe, expect, it } from "vitest";
import { ApiProblem } from "../api";
import { routeOf, scrubBreadcrumb, scrubEvent, withoutQuery } from "./crashReporting";

describe("crash reports", () => {
  it("leave out the user, the query string, cookies and the referrer", () => {
    const event: ErrorEvent = {
      type: undefined,
      user: { id: "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f", email: "erwin@example.test" },
      request: {
        url: "https://golfprogress.app/reset-password?token=secret#top",
        query_string: "token=secret",
        cookies: { a: "b" },
        headers: { Referer: "https://golfprogress.app/reset-password?token=secret", "User-Agent": "Firefox" },
      },
    };

    const scrubbed = scrubEvent(event);

    expect(scrubbed.user).toBeUndefined();
    expect(scrubbed.request).toEqual({
      url: "https://golfprogress.app/reset-password",
      headers: { "User-Agent": "Firefox" },
    });
  });

  it("tag an ApiProblem with its traceId", () => {
    const problem = new ApiProblem(500, { status: 500, traceId: "00-abc" });

    const scrubbed = scrubEvent({ type: undefined }, { originalException: problem });

    expect(scrubbed.tags).toEqual({ "api.status": 500, "api.traceId": "00-abc" });
  });

  it("drop console breadcrumbs, and query strings from the others", () => {
    expect(scrubBreadcrumb({ category: "console", message: "Signed in as erwin@example.test" })).toBeNull();
    expect(
      scrubBreadcrumb({ category: "navigation", data: { from: "/welcome", to: "/reset-password?token=secret" } }),
    ).toEqual({ category: "navigation", data: { from: "/welcome", to: "/reset-password" } });
    expect(scrubBreadcrumb({ category: "fetch", data: { url: "https://api.example.test/me/rounds?skip=0" } })).toEqual({
      category: "fetch",
      data: { url: "https://api.example.test/me/rounds" },
    });
  });

  it("group server errors by endpoint, not by id", () => {
    expect(routeOf("/me/rounds/3F2B8C1E-5D4A-4B6F-9E2D-1A7C8B9D0E1F/reflections")).toBe("/me/rounds/{id}/reflections");
    expect(withoutQuery("/me/rounds")).toBe("/me/rounds");
  });
});
