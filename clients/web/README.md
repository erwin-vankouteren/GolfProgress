# Web client

The GolfProgress web app.

**Status:** the API client exists (#63). The React app itself is created in #75 (app
shell, theme and navigation).

## Stack

TypeScript (strict). The app (#75) adds React 18, Vite, TanStack Query and React Router.
Tests with Vitest; end-to-end tests with Playwright.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Getting started

Needs Node.js (version in [`.nvmrc`](.nvmrc)).

```bash
cd clients/web
npm install          # also generates the API types
npm run typecheck
npm test
```

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
  refresh, so a wrong password stays a plain `401`. The provider (access token in memory,
  refresh through the HttpOnly cookie) is #69.
- **Errors:** `unwrap` returns the data or throws an `ApiProblem` (status, detail,
  `traceId`, and `messagesFor(field)` for validation errors), so components never inspect
  status codes.
- **TypeScript version:** pinned to 5.x. `openapi-typescript` supports `^5.x`, not the
  native TypeScript 7 compiler.
- **Open enums** (#134): `sessionType`, micro-win `category` and round `source` are plain
  strings in responses, because the API may add values at any time. Read them through
  `sessionTypeOf`, `microWinCategoryOf` and `roundSourceOf` (`src/api/knownValues.ts`),
  which return `"unknown"` for a value this build doesn't know.
