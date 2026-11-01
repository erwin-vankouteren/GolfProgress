import * as Sentry from "@sentry/react";
import type { Breadcrumb, ErrorEvent, EventHint } from "@sentry/react";
import { ApiProblem, type ServerError } from "../api";

/**
 * Crash and error reporting (#119), through Sentry's EU region: uncaught errors, React
 * render errors, and 5xx responses with the API's `traceId`, so a report can be found in
 * the API's logs. Off unless the build has a DSN (VITE_SENTRY_DSN), so development and
 * tests send nothing.
 *
 * No personal data leaves the browser: no user or IP address, no cookies, headers or
 * bodies, no console output, and no query strings, which is where the password reset
 * token travels. The filters below repeat what `dataCollection` turns off, in case a
 * default changes. What's sent: docs/legal/store-declarations.md → "Crash reports".
 */
export function startCrashReporting({
  dsn,
  environment,
  release,
}: {
  dsn?: string;
  environment?: string;
  release: string;
}) {
  if (!dsn) return;
  Sentry.init({
    dsn,
    environment,
    release,
    dataCollection: { userInfo: false, cookies: false, httpHeaders: false, httpBodies: [], urlQueryParams: false },
    // Errors only: no performance tracing, so no trace headers on calls to the API either.
    tracePropagationTargets: [],
    // Console output can hold anything; sessions aren't needed to report crashes.
    integrations: (defaults) => defaults.filter((i) => i.name !== "Console" && i.name !== "BrowserSession"),
    beforeSend: scrubEvent,
    beforeBreadcrumb: scrubBreadcrumb,
  });
}

/** For createRoot: React's render errors, including those an error boundary caught. */
export const reactErrorHandler = Sentry.reactErrorHandler;

/** Reports a 5xx from the API, grouped by endpoint rather than by each round's id. */
export function reportServerError({ method, path, status, traceId }: ServerError) {
  const endpoint = `${method} ${routeOf(path)}`;
  Sentry.captureMessage(`API ${status}: ${endpoint}`, {
    level: "error",
    tags: { "api.status": status, "api.traceId": traceId },
    fingerprint: ["api", endpoint, String(status)],
  });
}

/** /me/rounds/3f2b8c1e-… becomes /me/rounds/{id}. */
export function routeOf(path: string): string {
  return path.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, "{id}");
}

export function scrubEvent(event: ErrorEvent, hint?: EventHint): ErrorEvent {
  delete event.user;
  if (event.request) {
    if (event.request.url) event.request.url = withoutQuery(event.request.url);
    delete event.request.query_string;
    delete event.request.cookies;
    if (event.request.headers) delete event.request.headers.Referer;
  }
  // An ApiProblem that escaped: its traceId finds the request in the API's logs.
  const error = hint?.originalException;
  if (error instanceof ApiProblem && error.traceId) {
    event.tags = { ...event.tags, "api.status": error.status, "api.traceId": error.traceId };
  }
  return event;
}

export function scrubBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb | null {
  if (breadcrumb.category === "console") return null;
  const data = breadcrumb.data;
  if (data) {
    for (const key of ["url", "from", "to"]) {
      if (typeof data[key] === "string") data[key] = withoutQuery(data[key]);
    }
  }
  return breadcrumb;
}

/** A URL or path without its query and fragment. */
export function withoutQuery(url: string): string {
  return url.replace(/[?#].*$/, "");
}
