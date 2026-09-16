import { readFileSync } from 'node:fs';

const report = JSON.parse(readFileSync('artifacts/nuget-audit.json', 'utf8'));
const vulnerable =
  report.projects?.flatMap(
    (project) =>
      project.frameworks?.flatMap((framework) =>
        [
          ...(framework.topLevelPackages || []),
          ...(framework.transitivePackages || []),
        ].filter((pkg) => pkg.vulnerabilities?.length),
      ) || [],
  ) || [];
if (vulnerable.length)
  throw new Error(
    `Vulnerable NuGet packages: ${vulnerable.map((pkg) => pkg.id).join(', ')}`,
  );
console.log('NuGet dependency audit passed.');
