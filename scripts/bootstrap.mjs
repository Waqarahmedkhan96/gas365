import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import { spawnSync } from 'node:child_process';

if (!existsSync('.env')) {
  const template = readFileSync('.env.example', 'utf8');
  writeFileSync(
    '.env',
    template.replace(
      'POSTGRES_PASSWORD=',
      `POSTGRES_PASSWORD=${randomBytes(24).toString('hex')}`,
    ),
    { mode: 0o600 },
  );
  console.log('Created local .env with a random database password.');
} else if (/^POSTGRES_PASSWORD=\s*$/m.test(readFileSync('.env', 'utf8'))) {
  throw new Error(
    'Set POSTGRES_PASSWORD in .env or remove the empty .env and run bootstrap again.',
  );
}

if (!process.argv.includes('--env-only')) {
  const result = spawnSync(
    'docker',
    ['compose', 'up', '--build', '--wait', '--wait-timeout', '180'],
    { stdio: 'inherit' },
  );
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
  if (result.status === 0)
    console.log(
      'Gas365 is ready at http://localhost:8080 (or WEB_PORT from .env).',
    );
}
