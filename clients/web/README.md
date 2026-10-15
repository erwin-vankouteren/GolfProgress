# Web client

The GolfProgress web app.

**Status:** the app shell (#75) and sign-in (#69): Welcome, Create account, Sign in,
session restore and sign out. The features arrive in M2.

## Stack

React 19 + TypeScript (strict), built with Vite. TanStack Query for server state, React
Router for navigation, react-intl for all text. ESLint (with `jsx-a11y` for
accessibility) and Prettier. Tests with Vitest and Testing Library; end-to-end tests with
Playwright.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md) and
[`wireframes.md`](../../docs/clients/wireframes.md).

## Getting started

Needs Node.js (version in [`.nvmrc`](.nvmrc)).

```bash
cd clients/web
npm install          # also generates the API types
npm run dev          # http://localhost:5173, against the API at http://localhost:5068
npm test             # unit and component tests (Vitest)
npm run test:e2e     # end-to-end (Playwright; first run: npx playwright install chromium)
npm run lint
npm run build        # typecheck + production bundle in dist/
```

Point it at another API with `VITE_API_BASE_URL`, e.g. in a `.env.local` file (not committed).

## Structure

| Path | What |
|---|---|
| `src/main.tsx` | Entry point: API client, providers, router |
| `src/app/` | `App` (providers), `routes.tsx`, `ApiContext` |
| `src/layout/` | The app frame: header with **Log round**, sidebar that becomes a menu below 900 px, skip link |
| `src/pages/` | One page per section; empty states from the wireframes until the features land |
| `src/auth/` | Welcome, Create account and Sign in: local checks first, then the API's field messages, 401/409, and 429 with its `Retry-After` wait |
| `src/session/` | `Session` (sign-in state and tokens, below); `RequireSession` sends signed-out users to Welcome, and back afterwards |
| `e2e/` | Playwright tests against the production build, with the API mocked by `page.route` |
| `src/version/` | `UpdateGate`: shows "Update required" when this build is older than `GET /client-config`'s minimum. It never blocks start-up, and if the check fails the app carries on |
| `src/i18n/messages.ts` | Every user-facing string, through react-intl |
| `src/theme/` | `tokens.css` / `tokens.ts` (**generated** from `design/tokens.json`; don't edit) and `global.css` |
| `src/api/` | The generated, typed API client (below) |

**Conventions:**
- **Styling:** colours, spacing and type only through the token CSS variables
  (`var(--color-primary)`), never literal values.
- **Text:** all of it through `messages.ts`.
- **Interactive elements:** at least `var(--touch-target)` tall, with a visible focus ring.
- **Generated files** (`tokens.*`, `schema.d.ts`) are excluded from Prettier, so they stay
  identical to their generators' output.

## API client: `src/api`

`npm run generate:api` turns [`api/openapi.json`](../../api/) into TypeScript types
(`src/api/schema.d.ts`) with `openapi-typescript`. The file isn't committed: it's
regenerated on install and before every typecheck and test run, so it can't be stale.
Requests go through `openapi-fetch`, typed by those types.

```ts
import { createApiClient, unwrap, totalCount, ApiProblem } from "./api";

const api = createApiClient({ baseUrl, tokens: session });

const me = unwrap(await api.GET("/me"));                       // typed UserResponse
const page = await api.GET("/me/rounds", { params: { query: { skip: 0, take: 20 } } });
const rounds = unwrap(page);
const total = totalCount(page.response);                       // from X-Total-Count

try {
  unwrap(await api.POST("/me/rounds", { body: { playedAt, holes: 18, totalStrokes } }));
} catch (error) {
  if (error instanceof ApiProblem) error.messagesFor("playedAt");
}
```

- **Access token:** sent on every authenticated request. On a `401` the client asks the
  `AccessTokenProvider` to refresh once and retries the request, body included.
  Concurrent requests share one refresh. Sign-in requests never carry a token or trigger a
  refresh, so a wrong password stays a plain `401`. The provider is the `Session` (below).
- **Errors:** `unwrap` returns the data or throws an `ApiProblem` (status, detail,
  `traceId`, and `messagesFor(field)` for validation errors), so components never inspect
  status codes.
- **Open enums** (#134): `sessionType`, micro-win `category` and round `source` are plain
  strings in responses, because the API may add values at any time. Read them through
  `sessionTypeOf`, `microWinCategoryOf` and `roundSourceOf` (`src/api/knownValues.ts`),
  which return `"unknown"` for a value this build doesn't know.

## Versions to keep

- **TypeScript 5.x:** `openapi-typescript` supports `^5.x`, not the native TypeScript 7
  compiler.
- **ESLint 9:** `eslint-plugin-jsx-a11y` doesn't support ESLint 10 yet.

## Session: `src/session`

- **Access token:** in the `Session` object's memory only, never `localStorage` or
  `sessionStorage` (the tests check this).
- **Refresh token:** the API's HttpOnly `gp_refresh` cookie (#68); this code never sees it.
  Only `/auth/*` requests send credentials (`credentials: "include"`, with
  `X-Refresh-Token-Mode: cookie`).
- **Reload:** `main.tsx` calls `session.restore()`, which is a refresh. The app shows
  "Signing you in…" until it settles, then the app or Welcome.
- **Expiry:** a `401` triggers one shared refresh; if that fails, the session clears and
  the user lands on Welcome.
- **Sign out:** `POST /auth/logout` revokes the cookie, then the session and the query
  cache are cleared, even if the API can't be reached.
