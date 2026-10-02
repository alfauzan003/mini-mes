import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { CreateWorkOrderRequest, EquipmentDto, ProductDto } from '@/shared/api/types'
import { WorkOrderForm } from './WorkOrderForm'

const products: ProductDto[] = [
  {
    code: 'CATH-NCM811',
    name: 'Cathode NCM811',
    polarity: 'CATHODE',
    route: [
      { operation: 'MIX', name: 'Mixing', seq: 1, uom: 'kg' },
      { operation: 'COAT', name: 'Coating', seq: 2, uom: 'm' },
      { operation: 'CAL', name: 'Calendering', seq: 3, uom: 'm' },
      { operation: 'SLIT', name: 'Slitting', seq: 4, uom: 'm' },
    ],
  },
  {
    code: 'ANOD-GRAPHITE',
    name: 'Anode Graphite',
    polarity: 'ANODE',
    route: [
      { operation: 'MIX', name: 'Mixing', seq: 1, uom: 'kg' },
      { operation: 'COAT', name: 'Coating', seq: 2, uom: 'm' },
      { operation: 'CAL', name: 'Calendering', seq: 3, uom: 'm' },
      { operation: 'SLIT', name: 'Slitting', seq: 4, uom: 'm' },
    ],
  },
]

function machine(code: string, operation: EquipmentDto['operation']): EquipmentDto {
  return { code, name: code, operation, laneCount: null, status: 'IDLE', openRun: null }
}

const equipment: EquipmentDto[] = [
  machine('MX01', 'MIX'),
  machine('CT01', 'COAT'),
  machine('CT02', 'COAT'),
  machine('CP01', 'CAL'),
  machine('SL01', 'SLIT'),
]

function optionCodes(select: HTMLElement): string[] {
  return within(select)
    .getAllByRole('option')
    .filter((option) => (option as HTMLOptionElement).value !== '')
    .map((option) => option.textContent ?? '')
}

async function fillCommon(user: ReturnType<typeof userEvent.setup>, start: string, end: string) {
  await user.selectOptions(screen.getByLabelText('Product'), 'CATH-NCM811')
  await user.clear(screen.getByLabelText('Target quantity'))
  await user.type(screen.getByLabelText('Target quantity'), '8')
  await user.type(screen.getByLabelText('Planned start'), start)
  await user.type(screen.getByLabelText('Planned end'), end)
}

describe('WorkOrderForm', () => {
  it('lists only coaters for the coating step', async () => {
    const user = userEvent.setup()
    render(<WorkOrderForm products={products} equipment={equipment} onSubmit={vi.fn()} />)

    await user.selectOptions(screen.getByLabelText('Product'), 'CATH-NCM811')

    expect(optionCodes(screen.getByLabelText('Coating'))).toEqual(['CT01', 'CT02'])
  })

  it('blocks submit when end is before start', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<WorkOrderForm products={products} equipment={equipment} onSubmit={onSubmit} />)

    await fillCommon(user, '2026-10-05T10:00', '2026-10-04T10:00')
    await user.selectOptions(screen.getByLabelText('Mixing'), 'MX01')
    await user.selectOptions(screen.getByLabelText('Coating'), 'CT01')
    await user.selectOptions(screen.getByLabelText('Calendering'), 'CP01')
    await user.selectOptions(screen.getByLabelText('Slitting'), 'SL01')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Planned end must be after planned start')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits operations for every route step', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<WorkOrderForm products={products} equipment={equipment} onSubmit={onSubmit} />)

    await fillCommon(user, '2026-10-05T08:00', '2026-10-06T08:00')
    await user.selectOptions(screen.getByLabelText('Mixing'), 'MX01')
    await user.selectOptions(screen.getByLabelText('Coating'), 'CT02')
    await user.selectOptions(screen.getByLabelText('Calendering'), 'CP01')
    await user.selectOptions(screen.getByLabelText('Slitting'), 'SL01')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))
    const payload = onSubmit.mock.calls[0][0] as CreateWorkOrderRequest
    expect(payload.productCode).toBe('CATH-NCM811')
    expect(payload.targetQty).toBe(8)
    expect(payload.plannedStart).toBe(new Date('2026-10-05T08:00').toISOString())
    expect(payload.plannedEnd).toBe(new Date('2026-10-06T08:00').toISOString())
    expect(payload.operations).toEqual([
      { operation: 'MIX', equipmentCode: 'MX01' },
      { operation: 'COAT', equipmentCode: 'CT02' },
      { operation: 'CAL', equipmentCode: 'CP01' },
      { operation: 'SLIT', equipmentCode: 'SL01' },
    ])
  })

  it('blocks submit when a step has no equipment', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<WorkOrderForm products={products} equipment={equipment} onSubmit={onSubmit} />)

    await fillCommon(user, '2026-10-05T08:00', '2026-10-06T08:00')
    await user.selectOptions(screen.getByLabelText('Mixing'), 'MX01')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findAllByText('Select equipment', { selector: 'p' })).toHaveLength(3)
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('clears the equipment selects when the product changes', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<WorkOrderForm products={products} equipment={equipment} onSubmit={onSubmit} />)

    await fillCommon(user, '2026-10-05T08:00', '2026-10-06T08:00')
    await user.selectOptions(screen.getByLabelText('Mixing'), 'MX01')
    await user.selectOptions(screen.getByLabelText('Coating'), 'CT01')
    await user.selectOptions(screen.getByLabelText('Calendering'), 'CP01')
    await user.selectOptions(screen.getByLabelText('Slitting'), 'SL01')

    await user.selectOptions(screen.getByLabelText('Product'), 'ANOD-GRAPHITE')

    for (const step of ['Mixing', 'Coating', 'Calendering', 'Slitting']) {
      expect(screen.getByLabelText(step)).toHaveValue('')
    }

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findAllByText('Select equipment', { selector: 'p' })).toHaveLength(4)
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
