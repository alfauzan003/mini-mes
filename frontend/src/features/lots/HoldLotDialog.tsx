import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useHoldLot } from '@/features/quality/api'
import { REASON_MAX_LENGTH } from '@/features/quality/reason'
import type { LotDto } from '@/shared/api/types'

interface HoldLotDialogProps {
  lot: LotDto
  open: boolean
  onOpenChange: (open: boolean) => void
}

function HoldForm({ lot, onDone }: { lot: LotDto; onDone: () => void }) {
  const hold = useHoldLot(lot.lotId)
  const [reason, setReason] = useState('')
  const canConfirm = reason.trim() !== '' && !hold.isPending

  async function submit() {
    if (!canConfirm) return
    try {
      await hold.mutateAsync({ reason: reason.trim() })
      toast.success(`${lot.lotId} put on hold`)
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
      <div className="space-y-1.5">
        <label htmlFor="hold-reason" className="text-sm font-medium">
          Reason
        </label>
        <Input id="hold-reason" maxLength={REASON_MAX_LENGTH} value={reason} onChange={(event) => setReason(event.target.value)} />
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" disabled={!canConfirm}>
          Hold lot
        </Button>
      </div>
    </form>
  )
}

export function HoldLotDialog({ lot, open, onOpenChange }: HoldLotDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Hold {lot.lotId}</DialogTitle>
          <DialogDescription>Stop this lot from moving on until QC releases or scraps it.</DialogDescription>
        </DialogHeader>
        <HoldForm key={lot.lotId} lot={lot} onDone={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}
