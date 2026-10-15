# Web client

The GolfProgress web app.

**Status:** not started. The project is created in #75 (app shell, theme and navigation).

## Stack

React 18 + TypeScript (strict), built with Vite. TanStack Query for server state, React
Router for navigation, and an API client generated from [`api/openapi.json`](../../api/)
with `openapi-typescript` and `openapi-fetch` (#63). Tests with Vitest and Testing
Library, end-to-end tests with Playwright.

Full details: [`docs/architecture.md`](../../docs/architecture.md) → "Client architecture".
Screens: [`docs/clients/ux-design.md`](../../docs/clients/ux-design.md).

## Build and run

To be added with the project in #75: install, generate the API client, run, test and build.
