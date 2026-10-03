import type { LiveReadingDto } from '@/shared/api/types'

const FIFTEEN_MINUTES_MS = 15 * 60_000

/** Replaces the reading for the same (equipmentCode, parameter), or appends it when new. */
export function applyReading(list: LiveReadingDto[] | undefined, r: LiveReadingDto): LiveReadingDto[] {
  const current = list ?? []
  const index = current.findIndex((x) => x.equipmentCode === r.equipmentCode && x.parameter === r.parameter)
  if (index === -1) return [...current, r]
  return current.map((x, i) => (i === index ? r : x))
}

/** Appends a reading to a rolling buffer, dropping everything older than maxAgeMs. */
export function appendRecent(
  buffer: LiveReadingDto[] | undefined,
  r: LiveReadingDto,
  nowMs: number,
  maxAgeMs = FIFTEEN_MINUTES_MS,
): LiveReadingDto[] {
  const cutoff = nowMs - maxAgeMs
  return [...(buffer ?? []), r].filter((x) => Date.parse(x.at) >= cutoff)
}

/** Limits are inclusive: a value equal to low or high is within limits. */
export function isOutOfLimits(r: { value: number; low: number; high: number }): boolean {
  return r.value < r.low || r.value > r.high
}
