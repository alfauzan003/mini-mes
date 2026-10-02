import { useId, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { OutputLine } from '@/shared/api/types'
import { CarrierInput } from './ScanInput'

interface ProduceOutputFormProps {
  /** Show the carrier field (COAT, CAL). MIX produces slurry without a carrier. */
  withCarrier: boolean
  emptyCarriers: string[]
  uom: string
  submitLabel: string
  submitting: boolean
  onSubmit: (line: OutputLine) => void
}

/** Single-output form for MIX, CAL and one COAT roll (a "Doff roll"). */
export function ProduceOutputForm({
  withCarrier,
  emptyCarriers,
  uom,
  submitLabel,
  submitting,
  onSubmit,
}: ProduceOutputFormProps) {
  const id = useId()
  const [carrier, setCarrier] = useState('')
  const [good, setGood] = useState('')
  const [reject, setReject] = useState('')

  const goodQty = Number(good)
  const rejectQty = reject.trim() === '' ? 0 : Number(reject)
  const valid =
    good.trim() !== '' &&
    goodQty > 0 &&
    !Number.isNaN(rejectQty) &&
    rejectQty >= 0 &&
    (!withCarrier || carrier.trim() !== '')

  return (
    <form
      className="space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        if (!valid || submitting) return
        onSubmit({ carrierCode: withCarrier ? carrier.trim() : null, lane: null, goodQty, rejectQty })
      }}
    >
      {withCarrier && (
        <div className="space-y-1.5">
          <Label htmlFor={`${id}-carrier`} className="text-base">
            Empty carrier
          </Label>
          <CarrierInput
            id={`${id}-carrier`}
            value={carrier}
            suggestions={emptyCarriers}
            onChange={setCarrier}
          />
        </div>
      )}
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor={`${id}-good`} className="text-base">
            {`Good (${uom})`}
          </Label>
          <Input
            id={`${id}-good`}
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            className="min-h-12 text-base"
            value={good}
            onChange={(event) => setGood(event.target.value)}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor={`${id}-reject`} className="text-base">
            {`Reject (${uom})`}
          </Label>
          <Input
            id={`${id}-reject`}
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            className="min-h-12 text-base"
            value={reject}
            onChange={(event) => setReject(event.target.value)}
          />
        </div>
      </div>
      <Button type="submit" size="lg" className="min-h-12 w-full text-base" disabled={!valid || submitting}>
        {submitLabel}
      </Button>
    </form>
  )
}
