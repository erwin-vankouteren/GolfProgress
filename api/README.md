# API contract

[`openapi.json`](openapi.json) is the API's OpenAPI spec: the contract the web, iOS and
Android clients generate their API clients from. It's generated from the API and
committed, so a pull request that changes the API also shows exactly how the contract
changed.

Background: [`docs/architecture.md`](../docs/architecture.md) → "The API contract".

## Regenerating it

After changing anything that affects the API's surface (routes, request or response
types, status codes), regenerate the spec and commit it with your change:

```powershell
./api/generate-openapi.ps1            # builds the API, then writes api/openapi.json
./api/generate-openapi.ps1 -NoBuild   # reuse an existing Release build
```

It runs on Windows PowerShell 5.1 and PowerShell 7. It uses Swashbuckle's CLI, pinned in
[`.config/dotnet-tools.json`](../.config/dotnet-tools.json) to the same version as the
API's Swashbuckle package; the two must match, so update them together.

The CLI starts the API's host to read the document, so the script supplies placeholder
values for the settings `Program.cs` requires (connection string, JWT key) and switches
off the background event purge. Nothing connects to a database.

## CI keeps it honest

The API workflow regenerates the spec from its own build and **fails if the committed
file differs**, printing the difference. If that check fails on your pull request, run
the script and commit `api/openapi.json`.

## Changing the contract

Installed mobile apps can't be forced to update, so changes must be **additive**: new
endpoints, new optional fields, new enum values. Don't remove or rename fields, or change
what they mean. When a breaking change is unavoidable, add a new endpoint next to the old
one. See `docs/architecture.md` → "API evolution".
