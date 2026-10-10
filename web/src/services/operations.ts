import { apiRequest, createIdempotencyKey } from '@/services/apiClient'
import type { Job } from '@/services/contracts'

export interface OperationPlan {
  planHash: string
  expiresAtUtc: string
  changes: string[]
  warnings: string[]
}

export async function planOperation(area: string, action: string, resourceId: string | null, parameters: Record<string, string>) {
  return apiRequest<OperationPlan>(`${area}/${action}/plan`, {
    method: 'POST',
    body: JSON.stringify({ resourceId, parameters }),
  })
}

export async function enqueueOperation(
  area: string,
  action: string,
  resourceId: string | null,
  parameters: Record<string, string>,
  confirmed: boolean,
  planHash: string | null = null,
) {
  const idempotencyKey = createIdempotencyKey()
  return apiRequest<Job>(`${area}/${action}`, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify({ resourceId, parameters, planHash, confirmed, idempotencyKey }),
  })
}
