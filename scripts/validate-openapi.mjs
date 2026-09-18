import SwaggerParser from '@apidevtools/swagger-parser';
import { mkdir, writeFile } from 'node:fs/promises';

const response = await fetch(
  process.env.OPENAPI_URL || 'http://127.0.0.1:5080/openapi/v1.json',
);
if (!response.ok) throw new Error(`OpenAPI request failed: ${response.status}`);
const schema = await response.json();
await SwaggerParser.validate(structuredClone(schema));
if (!schema.paths['/api/health']?.get?.responses['503'])
  throw new Error('Missing health failure contract');
for (const [path, method] of [
  ['/api/auth/login', 'post'],
  ['/api/auth/me', 'get'],
  ['/api/auth/sessions/{id}', 'delete'],
  ['/api/workspace/tenant', 'put'],
  ['/api/workspace/branches', 'post'],
  ['/api/workspace/roles', 'post'],
  ['/api/workspace/members', 'post'],
  ['/api/workspace/audit', 'get'],
  ['/api/workspace/inventory/items', 'get'],
  ['/api/workspace/inventory/items', 'post'],
  ['/api/workspace/inventory/items/{id}/ledger', 'get'],
  ['/api/workspace/inventory/documents', 'post'],
  ['/api/workspace/inventory/documents/{id}/reverse', 'post'],
]) {
  if (!schema.paths[path]?.[method])
    throw new Error(`Missing access contract: ${method} ${path}`);
}
await mkdir('artifacts', { recursive: true });
await writeFile('artifacts/openapi.json', JSON.stringify(schema, null, 2));
console.log('OpenAPI validated and saved to artifacts/openapi.json');
