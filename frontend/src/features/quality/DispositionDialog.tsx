import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import type { Disposition, LotDto } from '@/shared/api/types'
import { useDisposition } from './api'

interface DispositionDialogProps {
  lot: LotDto
  open: boolean
  onOpenChange: (open: boolean) => void
}

const CHOICES: { value: Disposition; label: string; confirm: string }[] = [
  { value: 'RELEASE', label: 'Release', confirm: 'Release lot' },
  { value: 'SCRAP', label: 'Scrap', confirm: 'Scrap lot' },
]

function DispositionForm({ lot, onDone }: { lot: LotDto; onDone: () => void }) {
  const disposition = useDisposition(lot.lotId)
  // No default: the backend needs an explicit decision, and scrapping must never happen by omission.
  const [decision, setDecision] = useState<Disposition | null>(null)
  const [reason, setReason] = useState('')

  const canConfirm = decision !== null && reason.trim() !== '' && !disposition.isPending

  async function submit() {
    if (decision === null || !canConfirm) return
    try {
      await disposition.mutateAsync({ decision, reason: reason.trim() })
      toast.success(decision === 'SCRAP' ? `${lot.lotId} scrapped` : `${lot.lotId} released`)
      onDone()
    } catch {
      // The global mutation error toast already reported it; keep the dialog open for a retry.
    }
  }

  return (
    <form
      className="space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        void submit()
      }}
    >
      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">Decision</legend>
        <div className="flex gap-4">
          {CHOICES.map((choice) => (
            <label key={choice.value} className="flex min-h-10 items-center gap-2 text-base">
              <input
                type="radio"
                name="decision"
                value={choice.value}
                checked={decision === choice.value}
                onChange={() => setDecision(choice.value)}
              />
              {choice.label}
            </label>
          ))}
        </div>
      </fieldset>
      <div className="space-y-1.5">
        <label htmlFor="disposition-reason" className="text-sm font-medium">
          Reason
        </label>
        <Input id="disposition-reason" value={reason} onChange={(event) => setReason(event.target.value)} />
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onDone}>
          Cancel
        </Button>
        <Button
          type="submit"
          variant={decision === 'SCRAP' ? 'destructive' : 'default'}
          disabled={!canConfirm}
        >
          {CHOICES.find((choice) => choice.value === decision)?.confirm ?? 'Confirm disposition'}
        </Button>
      </div>
    </form>
  )
}

export function DispositionDialog({ lot, open, onOpenChange }: DispositionDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Disposition {lot.lotId}</DialogTitle>
          <DialogDescription>
            Release the held lot back to stock, or scrap it. The reason is recorded on the inspection.
          </DialogDescription>
        </DialogHeader>
        <DispositionForm key={lot.lotId} lot={lot} onDone={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}
