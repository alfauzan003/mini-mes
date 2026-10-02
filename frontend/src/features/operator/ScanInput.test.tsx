import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ScanInput } from './ScanInput'

describe('ScanInput', () => {
  it('submits trimmed upper-case code on Enter and clears', async () => {
    const onScan = vi.fn()
    render(<ScanInput label="Scan lot or carrier" onScan={onScan} />)
    const input = screen.getByLabelText('Scan lot or carrier')

    await userEvent.type(input, '  bb-0001 {Enter}')

    expect(onScan).toHaveBeenCalledTimes(1)
    expect(onScan).toHaveBeenCalledWith('BB-0001')
    expect(input).toHaveValue('')
    expect(input).toHaveFocus()
  })

  it('ignores empty submit', async () => {
    const onScan = vi.fn()
    render(<ScanInput label="Scan lot or carrier" onScan={onScan} />)

    await userEvent.type(screen.getByLabelText('Scan lot or carrier'), '   {Enter}')

    expect(onScan).not.toHaveBeenCalled()
  })
})
