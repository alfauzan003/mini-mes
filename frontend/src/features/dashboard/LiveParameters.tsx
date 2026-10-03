import { TriangleAlert } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { LiveReadingDto } from '@/shared/api/types'
import { isOutOfLimits } from '@/shared/realtime/readings'

export function LiveParameters({ readings }: { readings: LiveReadingDto[] }) {
  if (readings.length === 0) return <p className="text-xs text-muted-foreground">No readings</p>

  return (
    <dl className="space-y-0.5 text-sm">
      {readings.map((r) => {
        const out = isOutOfLimits(r)
        return (
          <div
            key={r.parameter}
            className="flex items-center justify-between gap-2"
            title={`Limits ${r.low} to ${r.high} ${r.unit}`}
          >
            <dt className="text-muted-foreground">{r.parameter}</dt>
            <dd className={cn('flex items-center gap-1 font-mono tabular-nums', out && 'font-semibold text-red-700')}>
              {out && <TriangleAlert className="size-3.5" aria-hidden="true" />}
              <span className={out ? 'text-red-700' : undefined}>{`${Number(r.value.toFixed(1))} ${r.unit}`}</span>
              {out && <span className="sr-only">Out of limits</span>}
            </dd>
          </div>
        )
      })}
    </dl>
  )
}
