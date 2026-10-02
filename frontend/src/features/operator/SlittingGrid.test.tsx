import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SlittingGrid } from './SlittingGrid'

const EMPTY_CARRIERS = ['PC-0001', 'PC-0002']

function renderGrid(onSubmit = vi.fn(), laneCount = 8) {
  render(<SlittingGrid laneCount={laneCount} emptyCarriers={EMPTY_CARRIERS} onSubmit={onSubmit} />)
  return onSubmit
}

describe('SlittingGrid', () => {
  it('renders one row per lane', () => {
    renderGrid()

    for (let lane = 1; lane <= 8; lane++) {
      expect(screen.getByLabelText(`Lane ${lane} carrier`)).toBeInTheDocument()
      expect(screen.getByLabelText(`Lane ${lane} good m`)).toBeInTheDocument()
      expect(screen.getByLabelText(`Lane ${lane} reject m`)).toBeInTheDocument()
    }
    expect(screen.queryByLabelText('Lane 9 carrier')).not.toBeInTheDocument()
  })

  it('flags good qty without carrier and disables submit', async () => {
    renderGrid()

    await userEvent.type(screen.getByLabelText('Lane 1 good m'), '100')

    expect(screen.getByText('Carrier required')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Produce' })).toBeDisabled()
  })

  it('flags carrier used twice', async () => {
    renderGrid()

    await userEvent.type(screen.getByLabelText('Lane 1 carrier'), 'pc-0001')
    await userEvent.type(screen.getByLabelText('Lane 1 good m'), '100')
    await userEvent.type(screen.getByLabelText('Lane 2 carrier'), 'PC-0001')
    await userEvent.type(screen.getByLabelText('Lane 2 good m'), '100')

    expect(screen.getAllByText('Carrier used twice')).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Produce' })).toBeDisabled()
  })

  it('emits lanes 1..8 with quantities', async () => {
    const onSubmit = renderGrid()

    for (let lane = 1; lane <= 8; lane++) {
      await userEvent.type(screen.getByLabelText(`Lane ${lane} carrier`), `PC-${String(lane).padStart(4, '0')}`)
      await userEvent.type(screen.getByLabelText(`Lane ${lane} good m`), String(lane * 10))
      await userEvent.type(screen.getByLabelText(`Lane ${lane} reject m`), String(lane))
    }
    await userEvent.click(screen.getByRole('button', { name: 'Produce' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    const lines = onSubmit.mock.calls[0][0]
    expect(lines.map((l: { lane: number }) => l.lane)).toEqual([1, 2, 3, 4, 5, 6, 7, 8])
    expect(lines[2]).toEqual({ carrierCode: 'PC-0003', lane: 3, goodQty: 30, rejectQty: 3 })
    expect(lines[7]).toEqual({ carrierCode: 'PC-0008', lane: 8, goodQty: 80, rejectQty: 8 })
  })

  it('sends no carrier for a reject-only lane', async () => {
    const onSubmit = renderGrid(vi.fn(), 2)

    await userEvent.type(screen.getByLabelText('Lane 1 carrier'), 'PC-0001')
    await userEvent.type(screen.getByLabelText('Lane 1 good m'), '50')
    await userEvent.type(screen.getByLabelText('Lane 2 reject m'), '5')
    await userEvent.click(screen.getByRole('button', { name: 'Produce' }))

    expect(onSubmit).toHaveBeenCalledWith([
      { carrierCode: 'PC-0001', lane: 1, goodQty: 50, rejectQty: 0 },
      { carrierCode: null, lane: 2, goodQty: 0, rejectQty: 5 },
    ])
  })
})
