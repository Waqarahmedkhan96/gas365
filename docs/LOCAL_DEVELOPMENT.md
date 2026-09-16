# Local development

## Toolchain

Use the exact .NET SDK from `global.json`, Node from `.node-version`, pnpm from `package.json`, and Docker Desktop with the Linux engine. The package manifests, pnpm lockfile and per-project NuGet lockfiles define the dependency versions. The .NET 10 and Node 24 lines are LTS; verify support when updating them using the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and [Node release schedule](https://github.com/nodejs/Release).

No global PostgreSQL installation is needed. The Compose configuration is for local development, binds published ports to loopback, and uses a dedicated persistent volume. It is not a production deployment specification.

## Start the complete platform

```sh
node scripts/bootstrap.mjs
```

The script creates `.env` from the template with a random local database password if absent, builds both images, and waits for PostgreSQL, API and web health checks. It never replaces existing credentials. Open [localhost:8080](http://localhost:8080). Ports are configurable in `.env`.

Do not share `.env`. The example has no usable password. A changed password does not automatically change an existing database volume's credentials. Stop services using `docker compose down`; this preserves the data volume. Removing a volume destroys its database and must be deliberate.

## Edit with hot reload

```sh
pnpm install --frozen-lockfile
dotnet restore --locked-mode
dotnet tool restore
node scripts/bootstrap.mjs --env-only
docker compose up -d --wait postgres
pnpm api
```

In a second terminal, run `pnpm dev` and open [localhost:5173](http://localhost:5173). The Vite proxy targets API port 5080; update its target if you customize that port. Stop the Compose API/web before using the same ports for host processes. Both commands run from the repository root.

If the SDK was installed into a user-local directory, prepend that directory to PATH for the current terminal. The initial Windows validation used `$env:LOCALAPPDATA/Gas365/dotnet`; no machine-wide SDK was replaced.

## Checks

```sh
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test
pnpm build
dotnet format --verify-no-changes --no-restore
dotnet build --no-restore --configuration Release
node scripts/dotnet.mjs test --no-build --configuration Release
pnpm db:migrate
pnpm validate:openapi
pnpm exec playwright install chromium
pnpm test:e2e
```

Integration tests create and drop an isolated random test database on the local PostgreSQL service, requiring CREATE DATABASE permission. `scripts/dotnet.mjs` supplies local environment values without printing credentials. Alternatively set `GAS365_TEST_DATABASE` explicitly. Tests fail rather than silently skip if PostgreSQL is unavailable. Never target production with this test account.

Browser tests expect the complete Compose platform. OpenAPI validation expects the Development API at port 5080. Override `WEB_URL` and `OPENAPI_URL` if needed. API liveness is `/health/live`; database readiness is `/health/ready`; the typed frontend contract is `/api/health`. Only Development exposes `/openapi/v1.json`.

## Migrations and environments

`InitialBaseline` is an empty EF migration that establishes migration history without product tables. Apply it explicitly using `pnpm db:migrate`. API replicas never auto-migrate on startup. Tests apply it to an empty database, reapply it and check model drift. There is no older product schema to upgrade yet.

Development, Staging and Production settings contain no credentials. Set `ConnectionStrings__Database` via the environment or a secret manager. Configure allowed hosts, HTTPS termination and isolated database credentials for a deployment. Authentication and business endpoints are outside Phase 01.

CI builds SHA-tagged images, starts Compose, runs the same checks, validates OpenAPI and browser journeys, audits dependencies and retains evidence. No production deployment is configured. Production approval, backup/restore, full security scanning and launch gates remain scheduled work.
