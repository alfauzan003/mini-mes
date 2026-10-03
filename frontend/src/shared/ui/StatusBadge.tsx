import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

type Tone = 'neutral' | 'blue' | 'green' | 'amber' | 'red' | 'muted'

const TONE_CLASSES: Record<Tone, string> = {
  neutral: 'border-slate-300 bg-slate-100 text-slate-700',
  blue: 'border-blue-200 bg-blue-100 text-blue-800',
  green: 'border-green-200 bg-green-100 text-green-800',
  amber: 'border-amber-200 bg-amber-100 text-amber-900',
  red: 'border-red-200 bg-red-100 text-red-800',
  muted: 'border-transparent bg-muted text-muted-foreground',
}

const TONE_BY_VALUE: Record<string, Tone> = {
  WAIT: 'neutral',
  IDLE: 'neutral',
  EMPTY: 'neutral',
  RUN: 'blue',
  RUNNING: 'blue',
  RELEASED: 'blue',
  FINISHED: 'green',
  COMPLETED: 'green',
  PASS: 'green',
  OK: 'green',
  NG: 'red',
  HOLD: 'amber',
  MAINTENANCE: 'amber',
  FAIL: 'red',
  DOWN: 'red',
  SCRAPPED: 'red',
  CONSUMED: 'muted',
  PLANNED: 'muted',
}

export function StatusBadge({ value }: { value: string }) {
  const tone = TONE_BY_VALUE[value] ?? 'neutral'
  return (
    <Badge variant="outline" className={cn('font-medium', TONE_CLASSES[tone])}>
      {value}
    </Badge>
  )
}
