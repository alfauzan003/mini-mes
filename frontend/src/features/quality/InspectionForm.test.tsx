import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { DefectCodeDto, InspectionSpecDto } from '@/shared/api/types'
import { InspectionForm } from './InspectionForm'

const specs: InspectionSpecDto[] = [
  { id: 'spec-thickness', productCode: 'CAT-A', operation: 'CAL', itemName: 'Thickness', unit: 'um', lsl: 118, usl: 122, seq: 1 },
  { id: 'spec-density', productCode: 'CAT-A', operation: 'CAL', itemName: 'Density', unit: 'g/cc', lsl: 3.4, usl: 3.6, seq: 2 },
]

const defectCodes: DefectCodeDto[] = [
  { code: 'CAL-THK', description: 'Thickness out of spec', operation: 'CAL' },
  { code: 'GEN-DMG', description: 'Surface damage', operation: null },
]

function setup(lotQty = 118, submitting = false) {
  const onSubmit = vi.fn()
  render(
    <InspectionForm
      specs={specs}
      defectCodes={defectCodes}
      lotQty={lotQty}
      uom="m"
      onSubmit={onSubmit}
      submitting={submitting}
    />,
  )
  return { onSubmit }
}

const submit = () => screen.getByRole('button', { name: 'Submit inspection' })

describe('InspectionForm', () => {
  it('shows the limits of each spec', () => {
    setup()
    expect(screen.getByText('118 – 122 um')).toBeInTheDocument()
    expect(screen.getByText('3.4 – 3.6 g/cc')).toBeInTheDocument()
    expect(screen.getByTestId('inspection-result')).toHaveTextContent('INCOMPLETE')
  })

  it('shows NG and FAIL when a value is out of limits and asks for a defect', async () => {
    setup()
    expect(screen.queryByLabelText('Defect code')).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Thickness'), '123')
    await userEvent.type(screen.getByLabelText('Density'), '3.5')

    expect(screen.getByTestId('judgment-spec-thickness')).toHaveTextContent('NG')
    expect(screen.getByTestId('judgment-spec-density')).toHaveTextContent('OK')
    expect(screen.getByTestId('row-spec-thickness')).toHaveAttribute('data-judgment', 'NG')
    expect(screen.getByTestId('inspection-result')).toHaveTextContent('FAIL')
    expect(screen.getByLabelText('Defect code')).toBeInTheDocument()
    expect(screen.getByLabelText('Reason')).toBeInTheDocument()
    expect(submit()).toBeDisabled()
  })

  it('accepts a decimal comma', async () => {
    setup()
    await userEvent.type(screen.getByLabelText('Thickness'), '119,5')
    expect(screen.getByTestId('judgment-spec-thickness')).toHaveTextContent('OK')
  })

  it('treats a value that is not a clean number as missing', async () => {
    setup()
    await userEvent.type(screen.getByLabelText('Thickness'), '1,2,3')
    await userEvent.type(screen.getByLabelText('Density'), '3,5')
    expect(screen.queryByTestId('judgment-spec-thickness')).toBeEmptyDOMElement()
    expect(screen.getByTestId('inspection-result')).toHaveTextContent('INCOMPLETE')
    expect(submit()).toBeDisabled()
  })

  it('disables submit while a submission is pending', async () => {
    const { onSubmit } = setup(118, true)
    await userEvent.type(screen.getByLabelText('Thickness'), '120')
    await userEvent.type(screen.getByLabelText('Density'), '3,5')

    expect(screen.getByTestId('inspection-result')).toHaveTextContent('PASS')
    expect(submit()).toBeDisabled()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('limits the reason length', async () => {
    setup()
    await userEvent.type(screen.getByLabelText('Thickness'), '123')
    expect(screen.getByLabelText('Reason')).toHaveAttribute('maxLength', '473')
  })

  it('disables submit until every value is entered', async () => {
    const { onSubmit } = setup()
    expect(submit()).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Thickness'), '120')
    expect(submit()).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Density'), '3,5')
    expect(screen.getByTestId('inspection-result')).toHaveTextContent('PASS')
    expect(submit()).toBeEnabled()

    await userEvent.click(submit())
    expect(onSubmit).toHaveBeenCalledWith({
      measurements: [
        { specId: 'spec-thickness', value: 120 },
        { specId: 'spec-density', value: 3.5 },
      ],
      defectCode: null,
      reason: null,
      rejectQty: null,
    })
  })

  it('submits values by spec id with the chosen defect and reason on FAIL', async () => {
    const { onSubmit } = setup()
    await userEvent.type(screen.getByLabelText('Thickness'), '123,5')
    await userEvent.type(screen.getByLabelText('Density'), '3.5')

    await userEvent.selectOptions(screen.getByLabelText('Defect code'), 'CAL-THK')
    expect(submit()).toBeDisabled()
    await userEvent.type(screen.getByLabelText('Reason'), '  Roll too thick at the tail  ')
    await userEvent.type(screen.getByLabelText('Reject qty'), '5,5')
    expect(submit()).toBeEnabled()

    await userEvent.click(submit())
    expect(onSubmit).toHaveBeenCalledWith({
      measurements: [
        { specId: 'spec-thickness', value: 123.5 },
        { specId: 'spec-density', value: 3.5 },
      ],
      defectCode: 'CAL-THK',
      reason: 'Roll too thick at the tail',
      rejectQty: 5.5,
    })
  })

  it('leaves the reject qty out when blank and blocks one above the lot qty', async () => {
    const { onSubmit } = setup(100)
    await userEvent.type(screen.getByLabelText('Thickness'), '130')
    await userEvent.type(screen.getByLabelText('Density'), '3.5')
    await userEvent.selectOptions(screen.getByLabelText('Defect code'), 'GEN-DMG')
    await userEvent.type(screen.getByLabelText('Reason'), 'Scratch')
    expect(submit()).toBeEnabled()

    await userEvent.type(screen.getByLabelText('Reject qty'), '101')
    expect(screen.getByText('Cannot exceed 100 m')).toBeInTheDocument()
    expect(submit()).toBeDisabled()

    await userEvent.clear(screen.getByLabelText('Reject qty'))
    await userEvent.click(submit())
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ defectCode: 'GEN-DMG', rejectQty: null }))
  })

  it('drops the defect fields from the request when the lot passes after a fail', async () => {
    const { onSubmit } = setup()
    await userEvent.type(screen.getByLabelText('Thickness'), '130')
    await userEvent.type(screen.getByLabelText('Density'), '3.5')
    await userEvent.selectOptions(screen.getByLabelText('Defect code'), 'CAL-THK')
    await userEvent.type(screen.getByLabelText('Reason'), 'typo')

    await userEvent.clear(screen.getByLabelText('Thickness'))
    await userEvent.type(screen.getByLabelText('Thickness'), '120')
    await userEvent.click(submit())

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ defectCode: null, reason: null, rejectQty: null }))
  })
})
