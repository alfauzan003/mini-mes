import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import type { Consumption, RunDto, RunInputDto } from '@/shared/api/types'

interface TrackOutDialogProps {
  run: RunDto
  open: boolean
  onOpenChange: (open: boolean) => void
  submitting: boolean
  onSubmit: (consumptions: Consumption[] | null) => void
}

/**
 * Calendering never consumes its roll: the roll is the output itself and stays in stock (or goes back to
 * stock if nothing was produced), so it is never listed and never sent.
 */
function consumableInputs(run: RunDto): RunInputDto[] {
  return run.inputs.filter((input) => !(run.operation === 'CAL' && input.role === 'PRIMARY'))
}

/** Slitting always uses up the whole electrode, so its consumed qty is fixed to the remaining qty. */
function isFixedInput(run: RunDto, input: RunInputDto): boolean {
  return run.operation === 'SLIT' && input.role === 'PRIMARY'
}

function TrackOutForm({ run, submitting, onSubmit, onCancel }: Omit<TrackOutDialogProps, 'open' | 'onOpenChange'> & { onCancel: () => void }) {
  const inputs = consumableInputs(run)
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(inputs.map((input) => [input.lotId, String(input.qty)])),
  )

  function errorFor(input: RunInputDto): string | null {
    if (isFixedInput(run, input)) return null
    const text = values[input.lotId] ?? ''
    const qty = Number(text)
    if (text.trim() === '' || Number.isNaN(qty) || qty < 0) return 'Enter a quantity of 0 or more'
    if (qty > input.qty) return `Cannot exceed ${input.qty} ${input.uom}`
    return null
  }

  const hasErrors = inputs.some((input) => errorFor(input) !== null)

  return (
    <form
      className="space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        if (hasErrors || submitting) return
        onSubmit(
          inputs.length === 0
            ? null
            : inputs.map((input) => ({ lotId: input.lotId, consumedQty: Number(values[input.lotId]) })),
        )
      }}
    >
      {inputs.length === 0 ? (
        <p className="text-sm text-muted-foreground">No consumption to record for this run.</p>
      ) : (
        <ul className="space-y-3">
          {inputs.map((input) => {
            const error = errorFor(input)
            const fixed = isFixedInput(run, input)
            return (
              <li key={input.lotId} className="space-y-1">
                <div className="flex items-center justify-between gap-3">
                  <label htmlFor={`consumed-${input.lotId}`} className="font-mono text-base">
                    {input.lotId}
                  </label>
                  <div className="flex items-center gap-2">
                    <Input
                      id={`consumed-${input.lotId}`}
                      type="number"
                      inputMode="decimal"
                      min={0}
                      step="any"
                      aria-label={`Consumed ${input.lotId}`}
                      aria-invalid={!!error || undefined}
                      className="min-h-12 w-32 text-base"
                      value={values[input.lotId] ?? ''}
                      readOnly={fixed}
                      onChange={(event) => setValues((current) => ({ ...current, [input.lotId]: event.target.value }))}
                    />
                    <span className="w-8 text-sm text-muted-foreground">{input.uom}</span>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">
                  Remaining {input.qty} {input.uom}
                  {fixed && ' (used up in full)'}
                </p>
                {error && <p className="text-sm text-destructive">{error}</p>}
              </li>
            )
          })}
        </ul>
      )}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" className="min-h-12" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" className="min-h-12" disabled={hasErrors || submitting}>
          Confirm track out
        </Button>
      </div>
    </form>
  )
}

export function TrackOutDialog({ open, onOpenChange, ...formProps }: TrackOutDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Track out {formProps.run.workOrderNumber}</DialogTitle>
          <DialogDescription>
            Confirm how much of each input was consumed. Anything left over returns to stock.
          </DialogDescription>
        </DialogHeader>
        <TrackOutForm {...formProps} onCancel={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}
