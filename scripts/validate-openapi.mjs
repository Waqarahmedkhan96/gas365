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
  ['/api/workspace/sales/customers', 'get'],
  ['/api/workspace/sales/customers', 'post'],
  ['/api/workspace/sales/customers/{id}', 'put'],
  ['/api/workspace/sales/price-lists', 'get'],
  ['/api/workspace/sales/price-lists/{id}/prices', 'post'],
  ['/api/workspace/sales/price-lists/{id}/prices/{priceId}', 'put'],
  ['/api/workspace/sales/drafts', 'get'],
  ['/api/workspace/sales/drafts', 'post'],
  ['/api/workspace/sales/drafts/{id}', 'put'],
  ['/api/workspace/sales/drafts/{id}/post', 'post'],
  ['/api/workspace/sales/posted', 'get'],
  ['/api/workspace/sales/posted/{id}/receipt', 'get'],
  ['/api/workspace/sales/posted/{id}/payments', 'post'],
  ['/api/workspace/sales/posted/{id}/reverse', 'post'],
  ['/api/workspace/sales/cylinder-policies/{id}', 'put'],
  ['/api/workspace/sales/customers/{id}/deposit-arrangement', 'put'],
  ['/api/workspace/sales/loans/{id}/return', 'post'],
  ['/api/workspace/sales/loans/{id}/refunds', 'post'],
  ['/api/workspace/sales/loans/{id}/approvals', 'post'],
  ['/api/workspace/purchasing/purchases/{id}/cost-payments', 'post'],
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
