# Deployment

Target: Railway (Hobby, ~$5/mo) or Render (Starter web service $7/mo + managed Postgres $7/mo) — either is a standard Dockerfile-based PaaS deploy. These steps assume Railway; the Render equivalents are noted inline since the Dockerfile and env vars are identical either way.

## One-time setup

1. **Create the Railway project** (railway.app → New Project → Empty Project), then add two services to it:
   - `salesdesk-api` — Deploy from Dockerfile, pointing at `infrastructure/docker/api.Dockerfile` with build context `.` (repo root).
   - `postgres` — Railway's built-in "Add PostgreSQL" plugin (or Render's Managed Postgres).
2. **Set environment variables on the `salesdesk-api` service** (Railway → service → Variables; Render → service → Environment):
   | Variable | Value |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__SalesDesk` | Railway's injected `${{Postgres.DATABASE_URL}}` reference (or the Render Postgres connection string) |
   | `Jwt__Secret` | A long random secret, generated once and stored only in the platform (`openssl rand -base64 48`) |
   | `Cors__AllowedOrigins__0` | The deployed frontend's URL, e.g. `https://app.salesdesk.com` |

   Never put any of these in `appsettings.json`, a GitHub Actions workflow file, or repository/Actions "variables" (non-secret) settings — only in the deployment platform's own secret store, per the guardrail in TASK-020. `appsettings.json` ships with empty placeholders for exactly this reason; the app throws at startup if `Jwt:Secret` is missing so a misconfigured deploy fails loudly instead of running with a default key.

3. **Generate a `RAILWAY_TOKEN`** (Railway → project → Settings → Tokens) and add it as a GitHub Actions **repository secret** (`Settings → Secrets and variables → Actions`) named `RAILWAY_TOKEN`. This is the only credential that lives in GitHub, and it only grants deploy access to this one Railway project.

## Continuous deployment

`.github/workflows/deploy-api.yml` runs on every push/PR:

1. `test` job — restores, builds, and runs the Domain + Application unit test suites.
2. `deploy` job — only on push to `main`, and only after `test` passes — installs the Railway CLI and runs `railway up`, which builds `infrastructure/docker/api.Dockerfile` on Railway's infrastructure and releases it.

Migrations run automatically: `Program.cs` calls `dbContext.Database.MigrateAsync()` unconditionally on startup (see [README.md](README.md)), so a new release migrates the production database itself — there's no separate migration step in the pipeline.

## Custom domain & TLS

Both Railway and Render provision a subdomain automatically (`*.up.railway.app` / `*.onrender.com`). To use `api.salesdesk.com`:

1. In the service's Settings → Networking → Custom Domain, add `api.salesdesk.com`.
2. Add the CNAME record the platform gives you at your DNS provider.
3. TLS is issued and renewed automatically (Let's Encrypt) once the CNAME resolves — no manual certificate management.

## Cost

| Item | Provider | Cost |
|---|---|---|
| API service | Railway Hobby | ~$5/mo |
| Postgres | Railway Hobby (included) | included above |
| — or — | | |
| API service | Render Starter | $7/mo |
| Postgres | Render Starter Postgres | $7/mo |

Keep any staging/preview environment on a free/sleeping tier (Railway's free trial credit, or Render's free web service, which sleeps when idle) so it doesn't add to the recurring bill — only the production service needs to be always-on.
