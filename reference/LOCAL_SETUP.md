# Local Setup for VS Code

## Required downloads

- Git
- GitHub account and GitHub CLI (optional but useful)
- VS Code
- Docker Desktop with WSL 2 integration on Windows
- .NET SDK matching the repository `global.json` (use supported LTS)
- Node.js supported LTS and Corepack/pnpm
- PostgreSQL client tools (optional; database itself runs in Docker locally)

Recommended VS Code extensions:

- C# Dev Kit
- ESLint
- Prettier
- Tailwind CSS IntelliSense
- Docker
- GitHub Actions
- Playwright Test for VS Code
- EditorConfig

Avoid installing PostgreSQL directly unless you need it outside Docker.

## Expected commands after Stage 1

```bash
cp .env.example .env
docker compose up -d postgres
dotnet restore
corepack enable
pnpm install --frozen-lockfile
dotnet test
pnpm lint
pnpm typecheck
pnpm test
docker compose up --build
```

The repository must document exact versions and provide one cross-platform bootstrap script where practical. Secrets and production credentials must never enter `.env.example` or Git.
