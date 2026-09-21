# Local development

## Toolchain

Use the exact .NET SDK from `global.json`, Node from `.node-version`, pnpm from `package.json`, and Docker Desktop with the Linux engine. The package manifests, pnpm lockfile and per-project NuGet lockfiles define the dependency versions. The .NET 10 and Node 24 lines are LTS; verify support when updating them using the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and [Node release schedule](https://github.com/nodejs/Release).

No global PostgreSQL installation is needed. The Compose configuration is for local development, binds published ports to loopback, and uses a dedicated persistent volume. It is not a production deployment specification.

## Start the complete platform

```sh
node scripts/bootstrap.mjs
```

The script creates `.env` from the template with a random local database password if absent, builds both images, runs the separate migration job, and waits for PostgreSQL, API and web health checks. It never replaces existing credentials. Open [localhost:8080](http://localhost:8080). Ports are configurable in `.env`.

Choose **Open workspace**, then **Create a workspace**. Register with an email and your own password, open the verification email in [the local inbox](http://localhost:8025), verify, and sign in. Mailpit captures local email without sending it externally. There are no default accounts or passwords. The owner can manage business settings, branches, team invitations, roles, sessions and activity. New team members receive a set-password link; existing accounts can switch workspaces after signing in.

Do not share `.env`. The example has no usable password. A changed password does not automatically change an existing database volume's credentials. Stop services using `docker compose down`; this preserves the data volume. Removing a volume destroys its database and must be deliberate.

## Application shell

After signing in, use the sidebar or **Search pages** (`Ctrl+K` / `Cmd+K`) to navigate. Owners can open `/workspace/showcase` to inspect shared components, sample tables and interface states. The header branch selector remembers a valid selection for that tenant; administration screens remain workspace-wide. Inventory balances and source warehouses follow the active branch; transfer destinations include other authorized branches. See [interface patterns](DESIGN_SYSTEM.md).

Browser acceptance tests run against the built Nginx container. Its HTML response and CSP header share a fresh style nonce for accessible dialogs; preserve this pairing when changing hosts. The Vite development server does not reproduce that CSP. Playwright uses two workers to keep local password hashing and accessibility scans within the platform response budget.

## Edit with hot reload

```sh
pnpm install --frozen-lockfile
dotnet restore --locked-mode
dotnet tool restore
node scripts/bootstrap.mjs --env-only
docker compose up -d --wait postgres mailpit
pnpm db:migrate
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

Browser tests expect the complete Compose platform, including Mailpit at port 8025, and create synthetic accounts. OpenAPI validation expects the Development API at port 5080. Override `WEB_URL` and `OPENAPI_URL` if needed. API liveness is `/health/live`; database readiness is `/health/ready`; the typed frontend contract is `/api/health`. Only Development exposes `/openapi/v1.json`.

## Inventory

Open `/workspace/inventory` as an owner. Create units, categories and warehouses through **Catalog setup**, add items, then post reconciled opening stock. See [inventory operating rules and contracts](INVENTORY.md) and the [Phase 04 checkpoint](PHASE_04_REPORT.md). Stock commands are permanent and use separate reversal records.

If Docker reports a Windows port reservation, select an available port in ignored `.env` and restart Compose. This machine currently uses database port 54320; the template default is 55432. Keep the existing database volume and password.

## Migrations and environments

Phase 05 adds `/workspace/purchases` and `/workspace/suppliers`. Create a supplier, then save a multi-line purchase draft and review its server-calculated allocation before posting. Purchases support cash payments, full reversal and PDF vouchers; see [purchasing contracts](PURCHASING.md). Apply `20260919072004_Purchasing` through the migration job before the new API starts. Existing owner roles gain purchasing permissions; no business data is seeded.

On Windows, if the pnpm `exec` wrapper cannot locate an installed executable, the equivalent local commands are `node node_modules/@playwright/test/cli.js test` and `node node_modules/prettier/bin/prettier.cjs --check .`. Use the pinned pnpm version in CI/containers. The desktop bundled pnpm used for Phase 05 host checks reports 11.19.0; the container uses the project's pinned 12.4.2 with the frozen lockfile.

Phase 03 requires no new database migration or API contract change. Rebuild the web image through bootstrap to install the shell and matching HTML/CSP configuration.

`InventoryLedger` adds catalog, locations, cylinder assets and immutable stock documents/movements. `InventoryGuards` adds database mutation guards and existing owner permissions. Both run before the updated API starts. No opening stock is seeded.

`InitialBaseline` establishes migration history. `IdentityAndTenancy` adds Identity accounts, tenants, memberships, branches, tenant roles, branch grants, sessions and audit events. Bootstrap runs a separate, one-shot `migrate` container before starting the API. Apply migrations manually using `pnpm db:migrate`; API replicas never auto-migrate during ordinary startup. Tests cover empty databases, upgrade from Phase 01, repeat application and model drift. The down migration removes identity/workspace data: restore a backup for recovery instead of rolling it back against valuable accounts.

Development, Staging and Production settings contain no credentials. Set `ConnectionStrings__Database` via the environment or a secret manager. See [authentication decisions](adr/003-authentication.md) for session settings. Production requires same-origin HTTPS routing, allowed hosts, trusted proxy configuration, a shared protected Data Protection key store, TLS SMTP and isolated database credentials. Configure `Mail__Host`, `Mail__Port`, `Mail__PublicOrigin`, `Mail__From` and optional `Mail__Username`/`Mail__Password` through the deployment environment. Never expose Mailpit publicly.

Provisioning limits default to 3 active branches, 10 active users and 3 active administrators including the owner. Override `Provisioning__MaxBranches`, `Provisioning__MaxUsers` and `Provisioning__MaxAdmins` before creating a tenant; existing tenants retain their stored limits. These are development defaults pending commercial plan decisions in Phase 10. The browser cannot change limits. Local Data Protection keys persist in the `identity-keys` volume; this local volume is not encrypted at rest. Removing it invalidates cookies and outstanding account links.

CI builds SHA-tagged images, starts Compose, runs the same checks, validates OpenAPI and browser journeys, audits dependencies and retains evidence. No production deployment is configured. Production approval, backup/restore, full security scanning and launch gates remain scheduled work.

## Phase 06 work in progress

After applying `CustomersAndPriceLists` and `SalesDrafts`, owners can use `/workspace/customers`, `/workspace/prices` and `/workspace/sales`. Add a customer and a named price list, configure prices for catalog items, then prepare a branch sales draft and review its server totals. Drafts do not post stock or money. See [sales contracts](SALES.md) for permissions, migration notes and remaining scope. The Phase 06 feature branch is not a completed release.
