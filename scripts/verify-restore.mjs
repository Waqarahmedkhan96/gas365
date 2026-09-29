// Proves a backup can be restored: dumps the local database, restores it into an isolated database, compares
// migration history and row counts, applies this release's migrations to the copy, then drops it.
// Usage: node scripts/verify-restore.mjs   (requires the local Compose platform and the pinned .NET SDK)
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const env = Object.fromEntries(
  readFileSync('.env', 'utf8')
    .split(/\r?\n/)
    .filter((line) => line && !line.startsWith('#'))
    .map((line) => [
      line.slice(0, line.indexOf('=')),
      line.slice(line.indexOf('=') + 1),
    ]),
);
const container = 'gas365-postgres-1';
const target = 'gas365_restore_verify';
const exec = (args, options = {}) =>
  execFileSync('docker', ['exec', container, ...args], {
    encoding: 'utf8',
    ...options,
  });
const psql = (database, sql) =>
  exec([
    'psql',
    '-v',
    'ON_ERROR_STOP=1',
    '-U',
    env.POSTGRES_USER,
    '-d',
    database,
    '-tAc',
    sql,
  ]).trim();
const tables = [
  '__EFMigrationsHistory',
  'Tenants',
  'SaleEntries',
  'PurchaseEntries',
  'StockMovements',
  'FinanceEntries',
  'CylinderLoanEntries',
  'OpeningBalanceEntries',
  'AuditEvents',
];
const counts = (database) =>
  Object.fromEntries(
    tables.map((t) => [
      t,
      Number(psql(database, `SELECT count(*) FROM "${t}"`)),
    ]),
  );

const started = Date.now();
exec([
  'pg_dump',
  '-U',
  env.POSTGRES_USER,
  '-d',
  env.POSTGRES_DB,
  '--format=custom',
  '--compress=9',
  '--file=/tmp/verify.dump',
]);
const size = Number(exec(['stat', '-c', '%s', '/tmp/verify.dump']).trim());
psql('postgres', `DROP DATABASE IF EXISTS ${target}`);
psql('postgres', `CREATE DATABASE ${target}`);
try {
  exec([
    'pg_restore',
    '-U',
    env.POSTGRES_USER,
    '-d',
    target,
    '--no-owner',
    '--exit-on-error',
    '/tmp/verify.dump',
  ]);
  const live = counts(env.POSTGRES_DB);
  const restored = counts(target);
  const mismatched = tables.filter((t) => live[t] !== restored[t]);
  // Applying this release's migrations to the restored copy must succeed and leave nothing pending.
  const connection = `Host=127.0.0.1;Port=${env.POSTGRES_PORT || '55432'};Database=${target};Username=${env.POSTGRES_USER};Password=${env.POSTGRES_PASSWORD}`;
  execFileSync(
    'dotnet',
    [
      'ef',
      'database',
      'update',
      '--project',
      'src/Gas365.Infrastructure',
      '--startup-project',
      'src/Gas365.Api',
      '--connection',
      connection,
    ],
    {
      stdio: 'ignore',
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Database: connection,
      },
    },
  );
  const migrated = Number(
    psql(target, 'SELECT count(*) FROM "__EFMigrationsHistory"'),
  );
  console.table(
    tables.map((t) => ({ table: t, live: live[t], restored: restored[t] })),
  );
  console.log(
    `Backup ${(size / 1024).toFixed(1)} KiB restored and migrated (${migrated} migrations) in ${((Date.now() - started) / 1000).toFixed(1)} s.`,
  );
  if (mismatched.length > 0)
    throw new Error(
      `Row counts differ after restore: ${mismatched.join(', ')}`,
    );
  if (migrated < live.__EFMigrationsHistory)
    throw new Error('Migration history was lost.');
  console.log('Restore verification passed.');
} finally {
  psql('postgres', `DROP DATABASE IF EXISTS ${target}`);
  exec(['rm', '-f', '/tmp/verify.dump']);
}
