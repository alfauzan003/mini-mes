import { describe, expect, it } from 'vitest'
import type { LiveReadingDto, ReadingPointDto } from '@/shared/api/types'
import { mergeSeries } from './series'

const T0 = Date.parse('2026-10-03T10:00:00Z')
const iso = (offsetSec: number) => new Date(T0 + offsetSec * 1000).toISOString()

function live(parameter: string, offsetSec: number, value: number): LiveReadingDto {
  return { equipmentCode: 'CT01', parameter, kind: 'TEMPERATURE', unit: 'C', value, low: 0, high: 100, at: iso(offsetSec) }
}

const history: ReadingPointDto[] = [
  { at: iso(0), value: 1 },
  { at: iso(10), value: 2 },
]

describe('mergeSeries', () => {
  it('merges recent points after the last history point', () => {
    const result = mergeSeries(history, [live('Temp', 10, 2), live('Temp', 20, 3), live('Temp', 30, 4)], 'Temp', T0)
    expect(result).toEqual([
      { t: T0, value: 1 },
      { t: T0 + 10_000, value: 2 },
      { t: T0 + 20_000, value: 3 },
      { t: T0 + 30_000, value: 4 },
    ])
  })

  it('drops recent points before from', () => {
    const result = mergeSeries([], [live('Temp', -60, 9), live('Temp', 5, 3)], 'Temp', T0)
    expect(result).toEqual([{ t: T0 + 5_000, value: 3 }])
  })

  it('ignores other parameters', () => {
    const result = mergeSeries(history, [live('Speed', 20, 99)], 'Temp', T0)
    expect(result.map((p) => p.value)).toEqual([1, 2])
  })

  it('sorts and removes duplicate timestamps', () => {
    const result = mergeSeries([], [live('Temp', 20, 3), live('Temp', 10, 2), live('Temp', 20, 3)], 'Temp', T0)
    expect(result.map((p) => p.t)).toEqual([T0 + 10_000, T0 + 20_000])
  })
})
