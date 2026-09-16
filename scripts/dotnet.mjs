import { existsSync, readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

if (existsSync('.env')) {
  const values = Object.fromEntries(
    readFileSync('.env', 'utf8')
      .split(/\r?\n/)
      .filter((line) => line && !line.startsWith('#'))
      .map((line) => {
        const separator = line.indexOf('=');
        return [line.slice(0, separator), line.slice(separator + 1)];
      }),
  );
  // Generated local passwords are hex; quote user-supplied connection values.
  const quote = (value) => `"${String(value).replaceAll('"', '""')}"`;
  const connection = `Host=127.0.0.1;Port=${values.POSTGRES_PORT || '55432'};Database=${quote(values.POSTGRES_DB)};Username=${quote(values.POSTGRES_USER)};Password=${quote(values.POSTGRES_PASSWORD)};Timeout=5`;
  process.env.ConnectionStrings__Database ??= connection;
  process.env.GAS365_TEST_DATABASE ??= connection;
  process.env.ASPNETCORE_URLS ??= `http://127.0.0.1:${values.API_PORT || '5080'}`;
}
process.env.ASPNETCORE_ENVIRONMENT ??= 'Development';
const result = spawnSync('dotnet', process.argv.slice(2), {
  stdio: 'inherit',
  env: process.env,
});
if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
