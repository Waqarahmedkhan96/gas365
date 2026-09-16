# Local setup

The working commands and exact toolchain are documented in [Local development](../docs/LOCAL_DEVELOPMENT.md).

Use Docker Desktop with the Linux engine, the pinned .NET SDK and Node version, and the pnpm version in `package.json`. Run `node scripts/bootstrap.mjs` from the repository root for the complete local platform. It creates a private `.env` with a random database password if absent.

Recommended editor extensions: C# Dev Kit, ESLint, Prettier, Docker, GitHub Actions, Playwright Test and EditorConfig. Tailwind tooling becomes relevant when the design system is introduced.

The supplied development pack remains an ignored archive. Root project state, maintained reference files and executable configuration are authoritative for current setup.
