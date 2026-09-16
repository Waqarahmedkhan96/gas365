import { z } from 'zod';

const healthSchema = z.object({
  status: z.enum(['Healthy', 'Degraded', 'Unhealthy']),
  service: z.string(),
  database: z.enum(['Healthy', 'Degraded', 'Unhealthy', 'Unknown']),
});

export async function getHealth() {
  const response = await fetch('/api/health', {
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok && response.status !== 503) {
    throw new Error('The service could not be reached.');
  }
  const health = healthSchema.parse(await response.json());
  if (response.status === 503 && health.status === 'Healthy') {
    throw new Error('The service returned an inconsistent status.');
  }
  return health;
}
