import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ParameterSeriesDto } from '@/shared/api/types'
import { ParameterTrend } from './ParameterTrend'

const T0 = Date.parse('2026-10-03T10:00:00Z')

const SERIES: ParameterSeriesDto = {
  parameter: 'Oven temperature',
  kind: 'TEMPERATURE',
  unit: 'C',
  low: 90,
  high: 110,
  points: [
    { at: new Date(T0).toISOString(), value: 98 },
    { at: new Date(T0 + 60_000).toISOString(), value: 101.5 },
  ],
}

describe('ParameterTrend', () => {
  it('titles the chart and summarises the latest value', () => {
    render(<ParameterTrend series={SERIES} recent={[]} fromMs={T0} />)
    expect(screen.getByRole('figure', { name: 'Oven temperature (C)' })).toBeInTheDocument()
    expect(screen.getByText(/Latest 101\.5 C/)).toBeInTheDocument()
  })

  it('says so when there are no readings', () => {
    render(<ParameterTrend series={{ ...SERIES, points: [] }} recent={[]} fromMs={T0} />)
    expect(screen.getByText('No readings in this range.')).toBeInTheDocument()
  })
})
