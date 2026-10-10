import { apiRequest, apiUrl, createIdempotencyKey } from '@/services/apiClient'
import type { Job, JobEvent } from '@/services/contracts'

export interface OperationPlan {
  planHash: string
  expiresAtUtc: string
  changes: string[]
  warnings: string[]
}

export async function planOperation(
  area: string,
  action: string,
  resourceId: string | null,
  parameters: Record<string, string>,
) {
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

export async function stageSecret(value?: string) {
  return apiRequest<{ token: string; expiresAtUtc: string }>('secrets/stage', {
    method: 'POST',
    body: JSON.stringify({ value: value || null, generate: !value }),
  })
}

export async function confirmedOperation(
  area: string,
  action: string,
  resourceId: string | null,
  parameters: Record<string, string>,
) {
  const plan = await planOperation(area, action, resourceId, parameters)
  return {
    plan,
    submit: () => enqueueOperation(area, action, resourceId, parameters, true, plan.planHash),
  }
}

export function subscribeToJob(
  jobId: string,
  handlers: { progress: (event: JobEvent) => void; error?: () => void },
) {
  const source = new EventSource(apiUrl(`jobs/${encodeURIComponent(jobId)}/events`), {
    withCredentials: true,
  })
  source.addEventListener('progress', (message) =>
    handlers.progress(JSON.parse(message.data) as JobEvent),
  )
  source.onerror = () => {
    source.close()
    handlers.error?.()
  }
  return () => source.close()
}
