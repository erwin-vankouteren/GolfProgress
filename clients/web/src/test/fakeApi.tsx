import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { vi } from "vitest";
import { createApiClient } from "../api";
import { App } from "../app/App";
import { routes } from "../app/routes";
import { currentLanguage } from "../i18n/language";
import { Session } from "../session/session";

/** Answers one request: keyed by "METHOD /path", e.g. "GET /me/dashboard". */
export type Handler = (request: Request) => Response | Promise<Response>;

export const testUserId = "3f2b8c1e-5d4a-4b6f-9e2d-1a7c8b9d0e1f";

/** A JSON response; a problem+json one for errors, as the API sends. */
export const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });

/**
 * Renders the whole app, signed in, at `path`, over a fake API answering from
 * `handlers`. /client-config always passes; anything unhandled is a 404.
 */
export function renderSignedIn(path: string, handlers: Record<string, Handler>) {
  const requests: Request[] = [];
  const all: Record<string, Handler> = {
    "GET /client-config": () => json(200, { minimumVersions: { ios: "1.0.0", android: "1.0.0", web: "1.0.0" } }),
    ...handlers,
  };

  const api = createApiClient({
    baseUrl: "https://api.example.test",
    tokens: { accessToken: () => "token", refreshAccessToken: async () => null },
    language: () => currentLanguage.locale,
    fetch: vi.fn(async (request: Request) => {
      requests.push(request);
      const handler = all[`${request.method} ${new URL(request.url).pathname}`];
      return handler ? handler(request) : json(404, { status: 404 });
    }),
  });
  const session = new Session({ accessToken: "token", userId: testUserId });
  session.attach(api);
  const router = createMemoryRouter(routes, { initialEntries: [path] });

  render(
    <App api={api} session={session} appVersion="1.0.0">
      <RouterProvider router={router} />
    </App>,
  );
  return { router, requests, user: userEvent.setup() };
}

/** How many requests went to `path`. */
export const countTo = (requests: Request[], path: string) =>
  requests.filter((r) => new URL(r.url).pathname === path).length;
