const MINUTE = 60_000
// Candidate tick spacings, in minutes; the smallest one giving at most MAX_TICKS ticks wins.
const STEPS = [1, 2, 3, 5, 10, 15, 30, 60, 120, 180, 240, 360]
const MAX_TICKS = 6

const labelFormat = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', hour12: false })

export interface TimeAxis {
  domain: [number, number]
  ticks: number[]
  format: (ms: number) => string
}

/**
 * X axis for a trend over [fromMs, toMs]: ticks on round local minutes, spaced so that their HH:mm labels never
 * repeat. The first tick is strictly after fromMs so a 24 h range does not label both ends with the same time.
 * The spacing depends only on [fromMs, toMs]; domainEndMs may extend the visible end (for a reading newer than
 * toMs) without changing it, which keeps the labels steady while live readings arrive.
 */
export function timeAxis(fromMs: number, toMs: number, domainEndMs: number = toMs): TimeAxis {
  const span = toMs - fromMs
  const step = (STEPS.find((s) => span / (s * MINUTE) <= MAX_TICKS) ?? STEPS.at(-1)!) * MINUTE
  const offset = new Date(fromMs).getTimezoneOffset() * MINUTE
  const ticks: number[] = []
  for (let t = Math.floor((fromMs - offset) / step) * step + step + offset; t <= domainEndMs; t += step) ticks.push(t)
  return { domain: [fromMs, domainEndMs], ticks, format: (ms) => labelFormat.format(new Date(ms)) }
}
