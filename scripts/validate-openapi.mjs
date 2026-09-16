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
await mkdir('artifacts', { recursive: true });
await writeFile('artifacts/openapi.json', JSON.stringify(schema, null, 2));
console.log('OpenAPI validated and saved to artifacts/openapi.json');
