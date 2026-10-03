import type { APIRequestContext } from '@playwright/test'

// Setup helpers that talk to the API directly (no UI), for the demo steps that would only repeat clicks.

type Method = 'GET' | 'POST'

async function call<T>(request: APIRequestContext, token: string | null, method: Method, url: string, body?: unknown): Promise<T> {
  const response = await request.fetch(url, {
    method,
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    data: body,
  })
  if (!response.ok()) {
    throw new Error(`${method} ${url} failed with ${response.status()}: ${await response.text()}`)
  }
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

interface LotDto {
  lotId: string
  type: string
  status: string
  productCode: string | null
  currentOperation: string | null
}

interface SpecDto {
  id: string
  itemName: string
  lsl: number
  usl: number
}

interface DefectCodeDto {
  code: string
  description: string
  operation: string | null
}

interface WorkOrderDto {
  id: string
  number: string
  status: string
  operations: { id: string; operation: string; equipmentCode: string }[]
}

interface RunDto {
  id: string
}

interface EquipmentDto {
  code: string
  status: 'IDLE' | 'RUNNING' | 'DOWN' | 'MAINTENANCE'
  openRun: RunDto | null
}

export async function token(request: APIRequestContext, username: string): Promise<string> {
  const response = await call<{ token: string }>(request, null, 'POST', '/api/auth/demo-login', { username })
  return response.token
}

/** Receives a new material lot and returns its lot ID. */
export async function registerMaterial(request: APIRequestContext, token: string, code: string, qty: number): Promise<string> {
  const lot = await call<LotDto>(request, token, 'POST', '/api/lots/materials', { materialCode: code, qty })
  return lot.lotId
}

/** Codes of the first `n` empty carriers of a type, so a rerun never reuses a carrier a previous run filled. */
export async function emptyCarriers(request: APIRequestContext, token: string, type: 'BB' | 'PC', n: number): Promise<string[]> {
  const carriers = await call<{ code: string }[]>(request, token, 'GET', `/api/carriers?type=${type}&status=EMPTY`)
  if (carriers.length < n) throw new Error(`Only ${carriers.length} empty ${type} carriers left; ${n} needed.`)
  return carriers.slice(0, n).map((carrier) => carrier.code)
}

/** Lot IDs of one type made for a work order, sorted (pancakes therefore come out in lane order). */
export async function lotsOf(request: APIRequestContext, token: string, workOrderNumber: string, type: string): Promise<string[]> {
  const lots = await call<LotDto[]>(
    request,
    token,
    'GET',
    `/api/lots?workOrder=${encodeURIComponent(workOrderNumber)}&type=${type}`,
  )
  return lots.map((lot) => lot.lotId).sort()
}

export function pancakesOf(request: APIRequestContext, token: string, workOrderNumber: string): Promise<string[]> {
  return lotsOf(request, token, workOrderNumber, 'PANCAKE')
}

export function workOrder(request: APIRequestContext, token: string, id: string): Promise<WorkOrderDto> {
  return call<WorkOrderDto>(request, token, 'GET', `/api/work-orders/${id}`)
}

async function specsFor(request: APIRequestContext, token: string, lotId: string): Promise<{ lot: LotDto; specs: SpecDto[] }> {
  const lot = await call<LotDto>(request, token, 'GET', `/api/lots/${encodeURIComponent(lotId)}`)
  if (!lot.productCode || !lot.currentOperation) throw new Error(`Lot ${lotId} has nothing to inspect.`)
  const specs = await call<SpecDto[]>(
    request,
    token,
    'GET',
    `/api/specs?product=${lot.productCode}&operation=${lot.currentOperation}`,
  )
  if (specs.length === 0) throw new Error(`No spec for ${lot.productCode} ${lot.currentOperation}.`)
  return { lot, specs }
}

// Measured values allow at most 4 decimals.
const round4 = (value: number) => Math.round(value * 10_000) / 10_000
const midpoint = (spec: SpecDto) => round4((spec.lsl + spec.usl) / 2)

/** Records a passing inspection: every spec item measured at the middle of its limits. */
export async function inspectPass(request: APIRequestContext, token: string, lotId: string): Promise<void> {
  const { specs } = await specsFor(request, token, lotId)
  const measurements = specs.map((spec) => ({ specId: spec.id, value: midpoint(spec) }))
  await call(request, token, 'POST', `/api/lots/${encodeURIComponent(lotId)}/inspections`, { measurements })
}

/**
 * Records a failing inspection: the last spec item is measured above its upper limit, the rest at their midpoint.
 * The defect code is one that applies to the lot's operation, preferably the one named after the failed item.
 * The lot goes to HOLD. Returns the defect code used.
 */
export async function inspectFail(request: APIRequestContext, token: string, lotId: string): Promise<string> {
  const { lot, specs } = await specsFor(request, token, lotId)
  const failed = specs[specs.length - 1]
  const measurements = specs.map((spec) => ({
    specId: spec.id,
    value: spec === failed ? round4(spec.usl + (spec.usl - spec.lsl) / 4) : midpoint(spec),
  }))

  const codes = await call<DefectCodeDto[]>(request, token, 'GET', `/api/defect-codes?operation=${lot.currentOperation}`)
  const forOperation = codes.filter((code) => code.operation === lot.currentOperation)
  const keyword = failed.itemName.split(' ')[0].toLowerCase()
  const defect = forOperation.find((code) => code.description.toLowerCase().includes(keyword)) ?? forOperation[0]
  if (!defect) throw new Error(`No defect code applies to ${lot.currentOperation}.`)

  await call(request, token, 'POST', `/api/lots/${encodeURIComponent(lotId)}/inspections`, {
    measurements,
    defectCode: defect.code,
    reason: `${failed.itemName} above the upper limit`,
    rejectQty: null,
  })
  return defect.code
}

interface OutputLine {
  carrierCode: string | null
  lane: number | null
  goodQty: number
  rejectQty: number
}

/** One whole run through the API: track in, produce the given outputs, track out (inputs used up in full). */
export async function runOperation(
  request: APIRequestContext,
  token: string,
  equipmentCode: string,
  workOrderOperationId: string,
  inputs: string[],
  outputs: OutputLine[],
): Promise<void> {
  const run = await call<RunDto>(request, token, 'POST', '/api/runs/track-in', { equipmentCode, workOrderOperationId, inputs })
  await call(request, token, 'POST', `/api/runs/${run.id}/outputs`, { outputs })
  await call(request, token, 'POST', `/api/runs/${run.id}/track-out`, { consumptions: null })
}

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/**
 * Waits until a machine is IDLE without an open run, so a rerun can start on it. A DOWN machine from an earlier
 * injected fault recovers by itself once the simulator clears the alarm (30-60 s); maintenance is ended and a
 * run left open by an aborted earlier run is tracked out. Needs an admin token (maintenance and track-out).
 */
export async function ensureIdle(request: APIRequestContext, adminToken: string, code: string, timeoutMs = 120_000): Promise<void> {
  const deadline = Date.now() + timeoutMs
  for (;;) {
    const equipment = await call<EquipmentDto>(request, adminToken, 'GET', `/api/equipment/${code}`)
    if (equipment.status === 'IDLE' && !equipment.openRun) return
    if (equipment.status === 'MAINTENANCE') {
      await call(request, adminToken, 'POST', `/api/equipment/${code}/maintenance/end`)
      continue
    }
    if (equipment.openRun) {
      await call(request, adminToken, 'POST', `/api/runs/${equipment.openRun.id}/track-out`, { consumptions: null })
      continue
    }
    if (Date.now() > deadline) throw new Error(`${code} is still ${equipment.status} after ${timeoutMs / 1000} s.`)
    await sleep(2_000)
  }
}
