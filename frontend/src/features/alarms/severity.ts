import type { AlarmDto, AlarmSeverity } from '@/shared/api/types'

export const SEVERITY_CLASSES: Record<AlarmSeverity, string> = {
  CRITICAL: 'border-red-200 bg-red-100 text-red-800',
  MAJOR: 'border-amber-200 bg-amber-100 text-amber-900',
  WARNING: 'border-slate-300 bg-slate-100 text-slate-700',
}

/** Cleared alarms keep their final duration; active ones grow with `now` (epoch ms). */
export function alarmDurationSeconds(alarm: AlarmDto, now: number): number {
  const end = alarm.clearedAt ? Date.parse(alarm.clearedAt) : now
  return Math.max(0, (end - Date.parse(alarm.raisedAt)) / 1000)
}
