# Gas365 project state

## Current phase

Phase 00 — Repository Bootstrap and Source Inventory: complete on 2026-09-16. Phase 01 has not started.

## Completed acceptance criteria

- Read the master prompt, repository rules, phase prompts and all supplied reference Markdown.
- Inspected the 47-sheet workbook structure and all 142 archived screenshots at overview resolution.
- Created the source inventory, requirement matrix, five ADRs, prioritized backlog, risk register, conventions, C4-style diagrams and explicit scope boundary.
- Recorded source contradictions and implementation gates.
- Validated maintained Markdown links, Git whitespace and staged content exclusions.

## Decisions

React/TypeScript SPA/PWA; ASP.NET Core modular monolith; PostgreSQL. Initial hosting uses an application service and separate database; provider choice is open. Same-origin cookie authentication is the planned default. Weighted-average costing applies to new transactions with immutable COGS. Posted records use reversal and replacement. Source business files remain ignored. Neutral project wording and configured contributor identity apply to commits.

## Open decisions

See [risk register](docs/RISKS.md): legal entity/name/domain; tax/invoice/FBR applicability; hosting/session details; costing bucket, rounding, backdating, returns and cutover; cylinder ownership/deposits/serial policy; payment advances and closed-period corrections; delivery scope; subscription provider; recovery objectives and production infrastructure.

## Verification and limitations

See [Phase 00 report](docs/PHASE_00_REPORT.md). Documentation checks passed. No application build, security runtime test, deployment or database migration exists in this phase. Workbook results were not recalculated or audited. Screenshot review establishes workflows, not hidden system behavior.

## Exact next prompt

Run **Prompt 01 — Monorepo and Local Platform** from [prompts/PHASE_PROMPTS.md](prompts/PHASE_PROMPTS.md). Execute only that phase and stop after its acceptance checks and project-state update.
