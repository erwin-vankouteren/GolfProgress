# API contract

`openapi.json` here will be the API's OpenAPI spec: the contract the web, iOS and
Android clients generate their API clients from. It's generated from the API and
committed, so a pull request that changes the API shows exactly how the contract changed.

**Status:** being set up in #61, which adds the spec, the command to regenerate it, and
the CI check that it's up to date.

Background: [`docs/architecture.md`](../docs/architecture.md) → "The API contract".
