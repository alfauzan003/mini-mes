import { describe, expect, it } from 'vitest'
import { timeAxis } from './timeAxis'

const NOW = Date.parse('2026-10-03T13:28:41Z')

describe('timeAxis', () => {
  it.each([15, 60, 360, 1440])('spans the whole %i-minute range', (minutes) => {
    const fromMs = NOW - minutes * 60_000
    const axis = timeAxis(fromMs, NOW)
    expect(axis.domain).toEqual([fromMs, NOW])
    expect(axis.ticks.length).toBeGreaterThanOrEqual(3)
    for (const t of axis.ticks) {
      expect(t).toBeGreaterThan(fromMs)
      expect(t).toBeLessThanOrEqual(NOW)
    }
    // The ticks cover most of the range, not just the last couple of minutes.
    expect(axis.ticks.at(-1)! - axis.ticks[0]).toBeGreaterThanOrEqual(minutes * 60_000 * 0.5)
  })

  it.each([15, 60, 360, 1440])('labels every tick of the %i-minute range differently', (minutes) => {
    const axis = timeAxis(NOW - minutes * 60_000, NOW)
    const labels = axis.ticks.map(axis.format)
    expect(new Set(labels).size).toBe(labels.length)
    for (const label of labels) expect(label).toMatch(/^\d{2}:\d{2}$/)
  })

  it('places ticks on round minutes', () => {
    const axis = timeAxis(NOW - 15 * 60_000, NOW)
    for (const t of axis.ticks) expect(new Date(t).getSeconds()).toBe(0)
  })
})
