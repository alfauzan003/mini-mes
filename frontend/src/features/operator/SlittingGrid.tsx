import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { OutputLine } from '@/shared/api/types'
import { CarrierInput } from './ScanInput'

interface SlittingGridProps {
  laneCount: number
  emptyCarriers: string[]
  onSubmit: (lines: OutputLine[]) => void
  submitting?: boolean
}

interface Row {
  carrier: string
  good: string
  reject: string
  touched: boolean
}

interface RowErrors {
  carrier?: string
  quantity?: string
}

function parseQty(text: string): number {
  return text.trim() === '' ? 0 : Number(text)
}

function validate(rows: Row[]): RowErrors[] {
  const carrierCounts = new Map<string, number>()
  for (const row of rows) {
    const carrier = row.carrier.trim()
    if (carrier) carrierCounts.set(carrier, (carrierCounts.get(carrier) ?? 0) + 1)
  }
  return rows.map((row) => {
    const good = parseQty(row.good)
    const reject = parseQty(row.reject)
    const carrier = row.carrier.trim()
    const errors: RowErrors = {}
    if (Number.isNaN(good) || Number.isNaN(reject) || good < 0 || reject < 0) errors.quantity = 'Enter a quantity of 0 or more'
    else if (good + reject <= 0) errors.quantity = 'Enter good or reject m'
    if (good > 0 && !carrier) errors.carrier = 'Carrier required'
    else if (carrier && (carrierCounts.get(carrier) ?? 0) > 1) errors.carrier = 'Carrier used twice'
    return errors
  })
}

/** One row per slitting lane: pancake carrier, good m and reject m. Emits lines for lanes 1..N. */
export function SlittingGrid({ laneCount, emptyCarriers, onSubmit, submitting = false }: SlittingGridProps) {
  const [rows, setRows] = useState<Row[]>(() =>
    Array.from({ length: laneCount }, () => ({ carrier: '', good: '', reject: '', touched: false })),
  )
  const errors = validate(rows)
  const hasErrors = errors.some((e) => e.carrier || e.quantity)

  function update(index: number, patch: Partial<Row>) {
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch, touched: true } : row)))
  }

  function handleSubmit() {
    if (hasErrors || submitting) return
    onSubmit(
      rows.map((row, index) => {
        const goodQty = parseQty(row.good)
        return {
          carrierCode: goodQty > 0 ? row.carrier.trim() : null,
          lane: index + 1,
          goodQty,
          rejectQty: parseQty(row.reject),
        }
      }),
    )
  }

  return (
    <form
      className="space-y-3"
      onSubmit={(event) => {
        event.preventDefault()
        handleSubmit()
      }}
    >
      <div className="space-y-2">
        {rows.map((row, index) => {
          const lane = index + 1
          const rowErrors = row.touched ? errors[index] : {}
          return (
            <div key={lane} className="grid grid-cols-[3rem_1fr_7rem_7rem] items-start gap-2">
              <div className="flex min-h-12 items-center text-lg font-semibold tabular-nums">L{lane}</div>
              <div className="space-y-1">
                <CarrierInput
                  aria-label={`Lane ${lane} carrier`}
                  value={row.carrier}
                  suggestions={emptyCarriers}
                  invalid={!!rowErrors.carrier}
                  onChange={(carrier) => update(index, { carrier })}
                />
                {rowErrors.carrier && <p className="text-sm text-destructive">{rowErrors.carrier}</p>}
              </div>
              <Input
                type="number"
                inputMode="decimal"
                min={0}
                step="any"
                placeholder="Good m"
                aria-label={`Lane ${lane} good m`}
                aria-invalid={!!rowErrors.quantity || undefined}
                className="min-h-12 text-base"
                value={row.good}
                onChange={(event) => update(index, { good: event.target.value })}
              />
              <Input
                type="number"
                inputMode="decimal"
                min={0}
                step="any"
                placeholder="Reject m"
                aria-label={`Lane ${lane} reject m`}
                aria-invalid={!!rowErrors.quantity || undefined}
                className="min-h-12 text-base"
                value={row.reject}
                onChange={(event) => update(index, { reject: event.target.value })}
              />
              {rowErrors.quantity && <p className="col-span-4 pl-14 text-sm text-destructive">{rowErrors.quantity}</p>}
            </div>
          )
        })}
      </div>
      <Button type="submit" size="lg" className="min-h-12 w-full text-base" disabled={hasErrors || submitting}>
        Produce
      </Button>
    </form>
  )
}
