import { describe, expect, it } from 'vitest'
import { judge, parseDecimal, summarize } from './judge'

describe('judge', () => {
  it('treats both limits as inclusive', () => {
    expect(judge(19.5, 19.5, 20.5)).toBe('OK')
    expect(judge(20.5, 19.5, 20.5)).toBe('OK')
    expect(judge(20, 19.5, 20.5)).toBe('OK')
  })

  it('is NG outside the limits', () => {
    expect(judge(20.6, 19.5, 20.5)).toBe('NG')
    expect(judge(19.4, 19.5, 20.5)).toBe('NG')
  })
})

describe('parseDecimal', () => {
  it('accepts a decimal comma and a decimal point', () => {
    expect(parseDecimal('19,8')).toBe(19.8)
    expect(parseDecimal('19.8')).toBe(19.8)
  })

  it('trims and accepts integers and negatives', () => {
    expect(parseDecimal(' 7900 ')).toBe(7900)
    expect(parseDecimal('-85.5')).toBe(-85.5)
    expect(parseDecimal('-85,5')).toBe(-85.5)
  })

  it('rejects ambiguous or non-numeric input', () => {
    expect(parseDecimal('1,2,3')).toBeNull()
    expect(parseDecimal('1.234,5')).toBeNull()
    expect(parseDecimal('1,234.5')).toBeNull()
    expect(parseDecimal('1.2.3')).toBeNull()
    expect(parseDecimal('abc')).toBeNull()
    expect(parseDecimal('12abc')).toBeNull()
    expect(parseDecimal('1e3')).toBeNull()
    expect(parseDecimal('Infinity')).toBeNull()
    expect(parseDecimal('')).toBeNull()
    expect(parseDecimal('   ')).toBeNull()
    expect(parseDecimal(',')).toBeNull()
  })
})

describe('summarize', () => {
  it('passes only when every judgment is OK', () => {
    expect(summarize(['OK', 'OK'])).toBe('PASS')
  })

  it('fails when any judgment is NG, even if others are missing', () => {
    expect(summarize(['OK', 'NG'])).toBe('FAIL')
    expect(summarize([null, 'NG'])).toBe('FAIL')
  })

  it('is incomplete while a value is missing and nothing has failed', () => {
    expect(summarize(['OK', null])).toBe('INCOMPLETE')
    expect(summarize([])).toBe('INCOMPLETE')
  })
})
