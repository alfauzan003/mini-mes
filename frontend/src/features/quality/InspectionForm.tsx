import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import type { DefectCodeDto, InspectionSpecDto, Judgment, RecordInspectionRequest } from '@/shared/api/types'
import { judge, parseDecimal, summarize } from './judge'

interface InspectionFormProps {
  specs: InspectionSpecDto[]
  defectCodes: DefectCodeDto[]
  lotQty: number
  uom: string
  onSubmit: (request: RecordInspectionRequest) => void
}

export function InspectionForm({ specs, defectCodes, lotQty, uom, onSubmit }: InspectionFormProps) {
  const [values, setValues] = useState<Record<string, string>>({})
  const [defectCode, setDefectCode] = useState('')
  const [reason, setReason] = useState('')
  const [rejectQty, setRejectQty] = useState('')

  const parsed = specs.map((spec) => parseDecimal(values[spec.id] ?? ''))
  const judgments: (Judgment | null)[] = specs.map((spec, index) => {
    const value = parsed[index]
    return value === null ? null : judge(value, spec.lsl, spec.usl)
  })
  const result = summarize(judgments)
  const allEntered = parsed.every((value) => value !== null)

  const rejectText = rejectQty.trim()
  const rejectValue = rejectText === '' ? null : parseDecimal(rejectText)
  const rejectError =
    rejectText === ''
      ? null
      : rejectValue === null || rejectValue < 0
        ? 'Enter a quantity of 0 or more'
        : rejectValue > lotQty
          ? `Cannot exceed ${lotQty} ${uom}`
          : null

  const failed = result === 'FAIL'
  const canSubmit =
    specs.length > 0 &&
    allEntered &&
    (result === 'PASS' || (failed && defectCode !== '' && reason.trim() !== '' && rejectError === null))

  return (
    <form
      className="space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        if (!canSubmit) return
        onSubmit({
          measurements: specs.map((spec, index) => ({ specId: spec.id, value: parsed[index] as number })),
          defectCode: failed ? defectCode : null,
          reason: failed ? reason.trim() : null,
          rejectQty: failed ? rejectValue : null,
        })
      }}
    >
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm text-muted-foreground">Result</span>
        <span data-testid="inspection-result">
          <StatusBadge value={result} />
        </span>
      </div>

      <ul className="space-y-3">
        {specs.map((spec, index) => {
          const judgment = judgments[index]
          return (
            <li
              key={spec.id}
              data-testid={`row-${spec.id}`}
              data-judgment={judgment ?? undefined}
              className={cn(
                'flex items-center justify-between gap-3 rounded-lg border p-3',
                judgment === 'NG' && 'border-red-300 bg-red-50',
              )}
            >
              <div className="min-w-0">
                <label htmlFor={`value-${spec.id}`} className="text-base font-medium">
                  {spec.itemName}
                </label>
                <p className="text-sm text-muted-foreground">
                  {spec.lsl} – {spec.usl} {spec.unit}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Input
                  id={`value-${spec.id}`}
                  type="text"
                  inputMode="decimal"
                  autoComplete="off"
                  className="min-h-12 w-32 text-base"
                  value={values[spec.id] ?? ''}
                  onChange={(event) => setValues((current) => ({ ...current, [spec.id]: event.target.value }))}
                />
                <span data-testid={`judgment-${spec.id}`} className="inline-flex w-10 justify-center">
                  {judgment && <StatusBadge value={judgment} />}
                </span>
              </div>
            </li>
          )
        })}
      </ul>

      {failed && (
        <div className="space-y-3 rounded-lg border border-red-200 p-3">
          <div className="space-y-1">
            <label htmlFor="defect-code" className="text-sm font-medium">
              Defect code
            </label>
            <NativeSelect
              id="defect-code"
              className="min-h-12 text-base"
              value={defectCode}
              onChange={(event) => setDefectCode(event.target.value)}
            >
              <option value="">Select a defect code</option>
              {defectCodes.map((defect) => (
                <option key={defect.code} value={defect.code}>
                  {defect.code} – {defect.description}
                </option>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <label htmlFor="inspection-reason" className="text-sm font-medium">
              Reason
            </label>
            <Input
              id="inspection-reason"
              className="min-h-12 text-base"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="reject-qty" className="text-sm font-medium">
              Reject qty
            </label>
            <div className="flex items-center gap-2">
              <Input
                id="reject-qty"
                type="text"
                inputMode="decimal"
                autoComplete="off"
                aria-invalid={rejectError ? true : undefined}
                className="min-h-12 w-32 text-base"
                value={rejectQty}
                onChange={(event) => setRejectQty(event.target.value)}
              />
              <span className="text-sm text-muted-foreground">{uom} (optional)</span>
            </div>
            {rejectError && <p className="text-sm text-destructive">{rejectError}</p>}
          </div>
        </div>
      )}

      <div className="flex justify-end">
        <Button type="submit" className="min-h-12" disabled={!canSubmit}>
          Submit inspection
        </Button>
      </div>
    </form>
  )
}
