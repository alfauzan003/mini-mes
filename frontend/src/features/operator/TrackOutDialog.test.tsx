import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { RunDto } from '@/shared/api/types'
import { TrackOutDialog } from './TrackOutDialog'

function calRun(produced: boolean): RunDto {
  return {
    id: 'run-1',
    equipmentCode: 'CL01',
    workOrderNumber: 'WO-261003-001',
    workOrderOperationId: 'op-3',
    operation: 'CAL',
    operator: 'operator',
    startedAt: '2026-10-03T01:00:00Z',
    endedAt: null,
    goodQty: 0,
    rejectQty: 0,
    parentLotId: 'EL-261003-001',
    inputs: [{ lotId: 'EL-261003-001', type: 'ELECTRODE', role: 'PRIMARY', qty: 118, uom: 'm', consumedQty: null }],
    outputs: produced ? [{ lotId: 'EL-261003-001', carrierCode: 'BB-0002', lane: null, goodQty: 118, rejectQty: 2 }] : [],
  }
}

function coatRun(): RunDto {
  return {
    ...calRun(false),
    operation: 'COAT',
    inputs: [
      { lotId: 'FO-261003-001', type: 'FOIL', role: 'PRIMARY', qty: 500, uom: 'm', consumedQty: null },
      { lotId: 'SL-261003-001', type: 'SLURRY', role: 'SECONDARY', qty: 40, uom: 'kg', consumedQty: null },
    ],
  }
}

describe('TrackOutDialog', () => {
  it('defaults each consumed qty to the remaining qty and submits edits', async () => {
    const onSubmit = vi.fn()
    render(<TrackOutDialog run={coatRun()} open onOpenChange={() => {}} submitting={false} onSubmit={onSubmit} />)

    expect(screen.getByLabelText('Consumed FO-261003-001')).toHaveValue(500)
    const slurry = screen.getByLabelText('Consumed SL-261003-001')
    expect(slurry).toHaveValue(40)

    await userEvent.clear(slurry)
    await userEvent.type(slurry, '25')
    await userEvent.click(screen.getByRole('button', { name: 'Confirm track out' }))

    expect(onSubmit).toHaveBeenCalledWith([
      { lotId: 'FO-261003-001', consumedQty: 500 },
      { lotId: 'SL-261003-001', consumedQty: 25 },
    ])
  })

  it('blocks a consumed qty above the remaining qty', async () => {
    render(<TrackOutDialog run={coatRun()} open onOpenChange={() => {}} submitting={false} onSubmit={vi.fn()} />)

    const slurry = screen.getByLabelText('Consumed SL-261003-001')
    await userEvent.clear(slurry)
    await userEvent.type(slurry, '41')

    expect(screen.getByText('Cannot exceed 40 kg')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirm track out' })).toBeDisabled()
  })

  it('hides the calendering primary once it has been produced', () => {
    render(<TrackOutDialog run={calRun(true)} open onOpenChange={() => {}} submitting={false} onSubmit={vi.fn()} />)

    expect(screen.queryByLabelText('Consumed EL-261003-001')).not.toBeInTheDocument()
  })

  it('still lists the calendering primary before it has been produced', () => {
    render(<TrackOutDialog run={calRun(false)} open onOpenChange={() => {}} submitting={false} onSubmit={vi.fn()} />)

    expect(screen.getByLabelText('Consumed EL-261003-001')).toHaveValue(118)
  })
})
