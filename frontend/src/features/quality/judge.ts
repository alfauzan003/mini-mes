import type { InspectionResult, Judgment } from '@/shared/api/types'

// One optional minus sign, digits, and at most one '.' or ',' as the decimal separator. Thousands groups,
// exponents, a second separator or any other character are ambiguous, so they are not numbers here.
const DECIMAL = /^-?\d*[.,]?\d+$/

/** Parses what a QC user typed; Indonesian locale writes a decimal comma. Null when it is not a clean number. */
export function parseDecimal(input: string): number | null {
  const text = input.trim()
  if (!DECIMAL.test(text)) return null
  const value = Number(text.replace(',', '.'))
  return Number.isFinite(value) ? value : null
}

/** Both limits are inclusive: a value sitting exactly on LSL or USL is OK. */
export function judge(value: number, lsl: number, usl: number): Judgment {
  return value >= lsl && value <= usl ? 'OK' : 'NG'
}

/** One NG fails the lot even while other items are still missing; otherwise every item must be judged OK. */
export function summarize(judgments: (Judgment | null)[]): InspectionResult | 'INCOMPLETE' {
  if (judgments.includes('NG')) return 'FAIL'
  if (judgments.length === 0 || judgments.includes(null)) return 'INCOMPLETE'
  return 'PASS'
}
