# GolfProgress

GolfProgress helps golfers see progress beyond their score. Every round gets a fitness
score built from four pillars (Execution, Damage control, Consistency and Habits), plus
micro-wins, a recommended next focus, and monthly reviews.

This repository holds the whole product: the back-end API and the web, iOS and Android
clients. How the parts fit together is described in
[`docs/architecture.md`](docs/architecture.md).

## Repository layout

| Path | What | Status |
|---|---|---|
| [`src/`](src/) | The API: ASP.NET Core 8 (`GolfProgress.Api`, `.Domain`, `.Infrastructure`) | In use |
| [`tests/`](tests/) | API tests | In use |
| [`api/`](api/) | The OpenAPI spec all clients are generated from | In use |
| [`clients/web/`](clients/web/) | Web app: React + TypeScript | Not started (#75) |
| [`clients/ios/`](clients/ios/) | iOS app: Swift + SwiftUI | Not started (#76) |
| [`clients/android/`](clients/android/) | Android app: Kotlin + Jetpack Compose | Not started (#77) |
| [`design/`](design/) | Design tokens shared by the clients | Not started (#73) |
| [`db/`](db/) | Database scripts and access model | In use |
| [`docs/`](docs/) | Architecture, UX design and deployment | In use |

Each folder has its own README with how to build and run that part.

## Getting started with the API

You need the .NET SDK version in [`global.json`](global.json), and a SQL Server database
(Azure SQL, or a local SQL Server or Docker container).

```powershell
# Secrets come from user-secrets locally, never from appsettings.json.
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>" --project src\GolfProgress.Api
dotnet user-secrets set "Jwt:Key" "<at least 32 random bytes>" --project src\GolfProgress.Api

dotnet run --project src\GolfProgress.Api      # Swagger UI at /swagger in Development
dotnet test                                    # the full test suite
```

- **Generating a JWT key** and every other setting: [`docs/deployment.md`](docs/deployment.md).
- **Database accounts and migrations:** [`db/README.md`](db/README.md).
- **SQL Server tests:** the tests under `tests/GolfProgress.Api.Tests/Persistence` start a
  SQL Server container. Without Docker they're skipped locally; in CI they always run.

## Documentation

- [`docs/architecture.md`](docs/architecture.md): the system, the decisions behind it, and the delivery phases.
- [`docs/clients/ux-design.md`](docs/clients/ux-design.md): screens and navigation for the clients.
- [`docs/deployment.md`](docs/deployment.md): settings and secrets for deployed environments.
- [`db/README.md`](db/README.md): database access, migrations and password rotation.

## Working on the code

- Work is tracked in [GitHub issues](https://github.com/erwin-vankouteren/GolfProgress/issues),
  grouped into epics and milestones (M1 Foundation → M4 Launch), with an `area:` label per part.
- Every change goes through a pull request from a branch. CI must pass before merging,
  and pull requests are merged with **rebase and merge**.
- Line endings are LF everywhere ([`.gitattributes`](.gitattributes), [`.editorconfig`](.editorconfig)).
