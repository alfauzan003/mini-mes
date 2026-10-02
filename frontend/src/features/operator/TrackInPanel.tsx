import { X } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import type { OperationCode } from '@/shared/api/types'
import { ScanInput } from './ScanInput'

const SCAN_HINT: Record<OperationCode, string> = {
  MIX: 'Scan one or more RAW lots.',
  COAT: 'Scan the foil lot, then the slurry lot.',
  CAL: 'Scan the BB carrier holding the electrode roll.',
  SLIT: 'Scan the BB carrier holding the electrode roll.',
}

interface TrackInPanelProps {
  operation: OperationCode
  submitting: boolean
  onTrackIn: (inputs: string[]) => void
}

/** Collects scanned lot IDs or carrier codes into a removable list, then submits them as one track-in. */
export function TrackInPanel({ operation, submitting, onTrackIn }: TrackInPanelProps) {
  const [scans, setScans] = useState<string[]>([])

  return (
    <div className="space-y-4">
      <ScanInput
        label="Scan lot or carrier"
        onScan={(code) => setScans((current) => (current.includes(code) ? current : [...current, code]))}
      />
      <p className="text-sm text-muted-foreground">{SCAN_HINT[operation]}</p>

      {scans.length > 0 && (
        <ul aria-label="Scanned inputs" className="space-y-2">
          {scans.map((code) => (
            <li key={code} className="flex items-center justify-between rounded-lg border bg-card px-3 py-1">
              <span className="font-mono text-base">{code}</span>
              <Button
                type="button"
                variant="ghost"
                className="min-h-12 min-w-12"
                aria-label={`Remove ${code}`}
                onClick={() => setScans((current) => current.filter((c) => c !== code))}
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <Button
        size="lg"
        className="min-h-12 w-full text-base"
        disabled={scans.length === 0 || submitting}
        onClick={() => onTrackIn(scans)}
      >
        Track in
      </Button>
    </div>
  )
}
