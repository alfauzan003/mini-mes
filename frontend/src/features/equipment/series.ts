import type { LiveReadingDto, ReadingPointDto } from '@/shared/api/types'

export interface TrendPoint {
  t: number
  value: number
}

/**
 * History plus the live readings of one parameter that arrived after it. Recent points at or before the
 * last history point or before fromMs are dropped; the result is sorted with unique timestamps.
 */
export function mergeSeries(
  history: ReadingPointDto[],
  recent: LiveReadingDto[],
  parameter: string,
  fromMs: number,
): TrendPoint[] {
  const points = history.map((p) => ({ t: Date.parse(p.at), value: p.value }))
  const lastHistory = points.reduce((max, p) => Math.max(max, p.t), -Infinity)
  for (const r of recent) {
    if (r.parameter !== parameter) continue
    const t = Date.parse(r.at)
    if (t > lastHistory && t >= fromMs) points.push({ t, value: r.value })
  }
  points.sort((a, b) => a.t - b.t)
  return points.filter((p, i) => i === 0 || p.t !== points[i - 1].t)
}
