import { describe, expect, it } from 'vitest'
import type { LiveReadingDto } from '@/shared/api/types'
import { appendRecent, applyReading, isOutOfLimits } from './readings'

function reading(overrides: Partial<LiveReadingDto> = {}): LiveReadingDto {
  return {
    equipmentCode: 'COAT-01',
    parameter: 'Oven temperature',
    kind: 'TEMPERATURE',
    unit: 'C',
    value: 100,
    low: 90,
    high: 110,
    at: '2026-10-03T10:00:00Z',
    ...overrides,
  }
}

describe('applyReading', () => {
  it('replaces the same equipment and parameter', () => {
    const other = reading({ equipmentCode: 'MIX-01' })
    const next = applyReading([reading(), other], reading({ value: 105 }))

    expect(next).toHaveLength(2)
    expect(next.find((r) => r.equipmentCode === 'COAT-01')?.value).toBe(105)
  })

  it('appends a new parameter', () => {
    const next = applyReading([reading()], reading({ parameter: 'Line speed' }))

    expect(next.map((r) => r.parameter)).toEqual(['Oven temperature', 'Line speed'])
    expect(applyReading(undefined, reading())).toHaveLength(1)
  })
})

describe('appendRecent', () => {
  it('drops readings older than 15 minutes', () => {
    const now = Date.parse('2026-10-03T10:30:00Z')
    const old = reading({ at: '2026-10-03T10:00:00Z' })
    const fresh = reading({ at: '2026-10-03T10:20:00Z' })
    const incoming = reading({ at: '2026-10-03T10:30:00Z' })

    expect(appendRecent([old, fresh], incoming, now)).toEqual([fresh, incoming])
  })
})

describe('isOutOfLimits', () => {
  it('is false on the limit itself', () => {
    expect(isOutOfLimits({ value: 110, low: 90, high: 110 })).toBe(false)
    expect(isOutOfLimits({ value: 90, low: 90, high: 110 })).toBe(false)
    expect(isOutOfLimits({ value: 110.1, low: 90, high: 110 })).toBe(true)
    expect(isOutOfLimits({ value: 89.9, low: 90, high: 110 })).toBe(true)
  })
})
