# Repository conventions

## Sources and progress

Root documentation and `reference/` are the maintained project specification. The original `Gas365-Development-Pack/` and business inputs remain ignored. Store new private inputs in `reference/inputs/`, also ignored. Never force-add customer records, screenshots, secrets or third-party assets. Use synthetic fixtures.

`PROJECT_STATE.md` records the completed phase, verification and unresolved decisions. Execute only the requested numbered phase and stop at its exit gate. Documentation and commit messages use neutral project wording without tool attribution or generated-by footers. Commit with the configured contributor identity; do not invent authors or co-author trailers.

## Planned layout

```text
apps/web
src/Gas365.Api
src/Gas365.Application
src/Gas365.Domain
src/Gas365.Infrastructure
tests/Gas365.UnitTests
tests/Gas365.IntegrationTests
tests/Gas365.ArchitectureTests
tests/Gas365.E2E
docs/adr
infra
reference
prompts
```

Application directories will be introduced in Phase 01. Pin SDK/runtime/package versions and commit lockfiles then. Use supported LTS releases verified at implementation time.

## Code and data rules

Use PascalCase for C# types, conventional TypeScript naming, clear module ownership and explicit DTOs. Controllers and React components do not own financial rules. Domain has no infrastructure dependencies; do not wrap EF Core in an unneeded generic repository. Use decimal values and documented scales for money/quantity/cost, UTC timestamps and explicit business dates/time zones. Resolve tenant ownership centrally and enforce it on every affected path.

## Changes and validation

Use focused branches such as `feat/inventory-ledger` and concise imperative commit messages. A review describes the problem, resulting behavior, tests, tenant/security effects, migrations and open limitations. Preserve unrelated work. Do not append tool branding to source or commits.

Run checks appropriate to scope. Documentation requires valid relative links, consistent traceability and a clean diff. Product changes require frontend lint/typecheck/tests, backend unit and real PostgreSQL integration tests, contract/architecture checks, appropriate Playwright journeys and empty/upgrade migration checks. Never substitute EF InMemory for relational behavior or weaken tests to hide failure.

Production requires a protected approval gate and controlled migration job. Test environments use separate databases and credentials. Do not run application scaffolding commands during Phase 00.
