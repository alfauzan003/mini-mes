import { describe, expect, it } from 'vitest'
import { formatDuration } from './duration'

describe('formatDuration', () => {
  it('formats seconds, minutes and hours', () => {
    expect(formatDuration(45)).toBe('45s')
    expect(formatDuration(185)).toBe('3m 05s')
    expect(formatDuration(3720)).toBe('1h 02m')
    expect(formatDuration(0)).toBe('0s')
  })
})
