# SalesDesk Backend

Standalone ASP.NET Core Web API for SalesDesk, structured with Clean Architecture. This repository is independent of the [salesdesk-frontend](../salesdesk-frontend) SPA — the two communicate only over the REST API, never by sharing code or a filesystem.

## Solution layout

```text
salesdesk-backend/
├── src/
│   ├── SalesDesk.Domain/          # Entities, value objects — no external dependencies
│   ├── SalesDesk.Application/     # Use cases (MediatR commands/queries), validation, DTOs
│   ├── SalesDesk.Infrastructure/  # EF Core persistence, auth, external integrations
│   └── SalesDesk.Api/             # ASP.NET Core host: controllers, DI wiring, Program.cs
├── tests/
│   ├── SalesDesk.Domain.Tests/
│   ├── SalesDesk.Application.Tests/
│   ├── SalesDesk.Infrastructure.Tests/
│   ├── SalesDesk.Api.Tests/
│   └── SalesDesk.IntegrationTests/
├── infrastructure/
│   ├── docker/api.Dockerfile
│   └── postgres/
├── .github/workflows/deploy-api.yml
├── docker-compose.yml             # Postgres + API for standalone local dev
└── SalesDesk.Backend.sln
```

Dependencies only point inward: `Api → Infrastructure → Application → Domain`.

## Running locally

**Option A — Docker Compose (Postgres + API together):**

```bash
JWT_SECRET="some-long-random-dev-secret" docker compose up --build
```

The API listens on `http://localhost:5000`, Postgres on `localhost:5432`.

**Option B — `dotnet run` against a local Postgres:**

```bash
dotnet restore SalesDesk.Backend.sln
dotnet run --project src/SalesDesk.Api
```

`appsettings.Development.json` already points at `localhost:5432` (matching `docker compose up postgres` run alone) with a dev-only JWT secret — no extra setup needed for local development. `appsettings.json` (used in every other environment) ships with **no** connection string or JWT secret; both must come from environment variables in staging/production (see [DEPLOYMENT.md](DEPLOYMENT.md)).

On every boot the API applies pending EF Core migrations automatically (`dbContext.Database.MigrateAsync()`); the Development environment additionally seeds demo data.

## Configuration

| Setting | Env var override | Purpose |
|---|---|---|
| `ConnectionStrings:SalesDesk` | `ConnectionStrings__SalesDesk` | PostgreSQL connection string |
| `Jwt:Secret` | `Jwt__Secret` | HMAC signing key for JWTs — **required**, the app fails fast at startup if unset |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `__1`, ... | Origins allowed to call the API cross-origin (the frontend's URL) |

## API contract

Swagger/OpenAPI is exposed in the Development environment at `/swagger/v1/swagger.json` (UI at `/swagger`), for generating a typed client or importing into API tooling.

## Tests

```bash
dotnet test SalesDesk.Backend.sln
```

`SalesDesk.Infrastructure.Tests`, `SalesDesk.Api.Tests`, and `SalesDesk.IntegrationTests` need a reachable Postgres instance (`docker compose up postgres`); `Domain.Tests` and `Application.Tests` are pure unit tests with no external dependencies.

## Deployment

See [DEPLOYMENT.md](DEPLOYMENT.md) for the Railway/Render setup, GitHub Actions pipeline, and required secrets.
