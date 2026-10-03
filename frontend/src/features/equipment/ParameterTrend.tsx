import { CartesianGrid, Line, LineChart, ReferenceLine, ResponsiveContainer, XAxis, YAxis } from 'recharts'
import type { LiveReadingDto, ParameterSeriesDto } from '@/shared/api/types'
import { mergeSeries } from './series'
import { timeAxis } from './timeAxis'

interface ParameterTrendProps {
  series: ParameterSeriesDto
  recent: LiveReadingDto[]
  fromMs: number
  toMs: number
}

export function ParameterTrend({ series, recent, fromMs, toMs }: ParameterTrendProps) {
  const data = mergeSeries(series.points, recent, series.parameter, fromMs)
  const title = `${series.parameter} (${series.unit})`
  const latest = data.at(-1)
  // A live reading can be a few seconds newer than the page clock; keep it on the chart.
  const axis = timeAxis(fromMs, Math.max(toMs, latest?.t ?? toMs))
  const summary = latest
    ? `Latest ${latest.value} ${series.unit} at ${axis.format(latest.t)}; limits ${series.low} to ${series.high}.`
    : 'No readings in this range.'

  return (
    <figure className="space-y-1 rounded-xl border bg-card p-4" aria-label={title}>
      <figcaption className="flex flex-wrap items-baseline justify-between gap-2">
        <span className="font-medium">{title}</span>
        <span className="text-sm text-muted-foreground">{summary}</span>
      </figcaption>
      <div className="h-48" role="img" aria-label={`${title} trend. ${summary}`}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
            <XAxis
              dataKey="t"
              type="number"
              scale="time"
              domain={axis.domain}
              ticks={axis.ticks}
              tickFormatter={axis.format}
              tick={{ fontSize: 12 }}
            />
            <YAxis domain={['auto', 'auto']} tick={{ fontSize: 12 }} width={48} />
            <ReferenceLine
              y={series.low}
              stroke="#d97706"
              strokeDasharray="4 4"
              ifOverflow="extendDomain"
              label={{ value: 'Low', fontSize: 11 }}
            />
            <ReferenceLine
              y={series.high}
              stroke="#dc2626"
              strokeDasharray="4 4"
              ifOverflow="extendDomain"
              label={{ value: 'High', fontSize: 11 }}
            />
            <Line
              type="monotone"
              dataKey="value"
              stroke="#2563eb"
              dot={false}
              strokeWidth={2}
              isAnimationActive={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </figure>
  )
}
