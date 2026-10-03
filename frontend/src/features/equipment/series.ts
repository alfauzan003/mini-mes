import type { LiveReadingDto, ReadingPointDto } from '@/shared/api/types'

export interface TrendPoint {
  t: number
  value: number
}

/**
 * History plus the live readings of one parameter that arrived after it. Recent points at or before the
 * last history point are dropped, and everything before fromMs is trimmed so the window slides; the result is sorted with unique timestamps.
 */
export function mergeSeries(
  history: ReadingPointDto[],
  recent: LiveReadingDto[],
  parameter: string,
  fromMs: number,
): TrendPoint[] {
  const points = history.map((p) => ({ t: Date.parse(p.at), value: p.value }))
  // Compute before trimming so the live-point rule still compares against the newest history point.
  const lastHistory = points.reduce((max, p) => Math.max(max, p.t), -Infinity)
  for (const r of recent) {
    if (r.parameter !== parameter) continue
    const t = Date.parse(r.at)
    if (t > lastHistory && t >= fromMs) points.push({ t, value: r.value })
  }
  const windowed = points.filter((p) => p.t >= fromMs)
  windowed.sort((a, b) => a.t - b.t)
  return windowed.filter((p, i) => i === 0 || p.t !== windowed[i - 1].t)
}
