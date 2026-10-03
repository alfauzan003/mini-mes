import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import type { EquipmentDto, LiveReadingDto, RunDto } from '@/shared/api/types'
import { EquipmentTile } from './EquipmentTile'

const RUN = {
  id: 'run-1',
  equipmentCode: 'MX-01',
  workOrderNumber: 'WO-261003-001',
  parentLotId: 'SL-261003-001',
  endedAt: null,
} as RunDto

const EQUIPMENT: EquipmentDto = {
  code: 'MX-01',
  name: 'Planetary Mixer 1',
  operation: 'MIX',
  laneCount: null,
  status: 'RUNNING',
  openRun: RUN,
}

const TEMP: LiveReadingDto = {
  equipmentCode: 'MX-01',
  parameter: 'Temperature',
  kind: 'TEMPERATURE',
  unit: 'C',
  value: 25.5,
  low: 20,
  high: 30,
  at: '2026-10-03T00:00:00Z',
}

function renderTile(props: Partial<Parameters<typeof EquipmentTile>[0]> = {}) {
  return render(
    <MemoryRouter>
      <EquipmentTile equipment={EQUIPMENT} readings={[TEMP]} activeAlarmCount={0} {...props} />
    </MemoryRouter>,
  )
}

describe('EquipmentTile', () => {
  it('shows work order and parent lot of the open run', () => {
    renderTile()

    expect(screen.getByText('MX-01')).toBeInTheDocument()
    expect(screen.getByText('Planetary Mixer 1')).toBeInTheDocument()
    expect(screen.getByText('WO-261003-001')).toBeInTheDocument()
    expect(screen.getByText('SL-261003-001')).toBeInTheDocument()
    expect(screen.getByRole('link')).toHaveAttribute('href', '/equipment/MX-01')
  })

  it('shows "No run" without an open run', () => {
    renderTile({ equipment: { ...EQUIPMENT, status: 'IDLE', openRun: null } })

    expect(screen.getByText('No run')).toBeInTheDocument()
  })

  it('marks out-of-limit reading red', () => {
    renderTile({ readings: [{ ...TEMP, value: 35 }] })

    const value = screen.getByText('35 C')
    expect(value).toHaveClass('text-red-700')
    expect(value.closest('[title]')).toHaveAttribute('title', expect.stringContaining('20'))
    expect(screen.getByText('Out of limits')).toBeInTheDocument()
  })

  it('keeps in-limit reading neutral', () => {
    renderTile()

    expect(screen.getByText('25.5 C')).not.toHaveClass('text-red-700')
    expect(screen.queryByText('Out of limits')).not.toBeInTheDocument()
  })

  it.each(['IDLE', 'DOWN', 'MAINTENANCE'] as const)('does not flag out-of-limit readings while %s', (status) => {
    renderTile({ equipment: { ...EQUIPMENT, status }, readings: [{ ...TEMP, value: 5 }] })

    expect(screen.getByText('5 C')).not.toHaveClass('text-red-700')
    expect(screen.queryByText('Out of limits')).not.toBeInTheDocument()
  })

  it('shows DOWN status and alarm count', () => {
    renderTile({ equipment: { ...EQUIPMENT, status: 'DOWN' }, activeAlarmCount: 2 })

    expect(screen.getByText('DOWN')).toBeInTheDocument()
    expect(screen.getByText('2 alarms')).toBeInTheDocument()
  })
})
