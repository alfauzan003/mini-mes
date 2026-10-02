const dateTime = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

export function formatDateTime(iso: string): string {
  return dateTime.format(new Date(iso))
}

export function progressPercent(good: number, target: number): number {
  return target > 0 ? Math.min(100, Math.round((good / target) * 100)) : 0
}
