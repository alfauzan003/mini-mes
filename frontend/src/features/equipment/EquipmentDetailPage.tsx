import { useState } from 'react'
import { useParams } from 'react-router'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAlarms } from '@/features/alarms/api'
import { useEquipmentDetail } from '@/features/operator/api'
import { formatDateTime } from '@/features/work-orders/formatters'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatDuration } from '@/shared/format/duration'
import { useNow } from '@/shared/realtime/useNow'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import {
  useEndMaintenance,
  useInjectFault,
  useParameterSeries,
  useRecentReadings,
  useStartMaintenance,
  useStatusLog,
} from './api'
import { ParameterTrend } from './ParameterTrend'

const RANGES = [
  { minutes: 15, label: '15 m' },
  { minutes: 60, label: '1 h' },
  { minutes: 360, label: '6 h' },
  { minutes: 1440, label: '24 h' },
]

function TrendsTab({ code }: { code: string }) {
  const [rangeMinutes, setRangeMinutes] = useState(15)
  const series = useParameterSeries(code, rangeMinutes)
  const recent = useRecentReadings(code)
  const now = useNow(5_000)
  const fromMs = now - rangeMinutes * 60_000

  return (
    <div className="space-y-4">
      <div className="w-40">
        <NativeSelect
          aria-label="Range"
          value={rangeMinutes}
          onChange={(event) => setRangeMinutes(Number(event.target.value))}
        >
          {RANGES.map((r) => (
            <option key={r.minutes} value={r.minutes}>
              {r.label}
            </option>
          ))}
        </NativeSelect>
      </div>
      {series.isError && <p className="text-sm text-destructive">Could not load trends.</p>}
      {series.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
      {series.data?.length === 0 && <p className="text-sm text-muted-foreground">No parameters for this machine.</p>}
      <div className="grid gap-4 lg:grid-cols-2">
        {series.data?.map((s) => (
          <ParameterTrend key={s.parameter} series={s} recent={recent.data} fromMs={fromMs} />
        ))}
      </div>
    </div>
  )
}

function StatusLogTab({ code }: { code: string }) {
  const log = useStatusLog(code)
  if (log.isError) return <p className="text-sm text-destructive">Could not load the status log.</p>
  if (log.isPending) return <p className="text-sm text-muted-foreground">Loading...</p>
  if (log.data.length === 0) return <p className="text-sm text-muted-foreground">No status changes yet.</p>
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>From</TableHead>
          <TableHead>To</TableHead>
          <TableHead>Reason</TableHead>
          <TableHead>Time</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {log.data.map((entry) => (
          <TableRow key={`${entry.changedAt}-${entry.from}-${entry.to}`}>
            <TableCell>
              <StatusBadge value={entry.from} />
            </TableCell>
            <TableCell>
              <StatusBadge value={entry.to} />
            </TableCell>
            <TableCell>{entry.reason}</TableCell>
            <TableCell>{formatDateTime(entry.changedAt)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

function AlarmsTab({ code }: { code: string }) {
  const alarms = useAlarms({ equipment: code })
  if (alarms.isError) return <p className="text-sm text-destructive">Could not load alarms.</p>
  if (alarms.isPending) return <p className="text-sm text-muted-foreground">Loading...</p>
  if (alarms.data.length === 0) return <p className="text-sm text-muted-foreground">No alarms for this machine.</p>
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Severity</TableHead>
          <TableHead>Message</TableHead>
          <TableHead>Raised</TableHead>
          <TableHead>Duration</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {alarms.data.map((a) => (
          <TableRow key={a.id}>
            <TableCell>{a.severity}</TableCell>
            <TableCell>{a.message}</TableCell>
            <TableCell>{formatDateTime(a.raisedAt)}</TableCell>
            <TableCell>{a.durationSeconds === null ? 'Active' : formatDuration(a.durationSeconds)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

function AdminActions({ code, status }: { code: string; status: string }) {
  const start = useStartMaintenance(code)
  const end = useEndMaintenance(code)
  const inject = useInjectFault(code)
  const [confirming, setConfirming] = useState(false)

  async function injectFault() {
    try {
      await inject.mutateAsync()
      toast.success(`Fault requested on ${code}`)
      setConfirming(false)
    } catch {
      // The global mutation error toast reports it (SIMULATOR_OFFLINE shows the API message and code).
      setConfirming(false)
    }
  }

  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="outline" disabled={status !== 'IDLE' || start.isPending} onClick={() => start.mutate()}>
        Start maintenance
      </Button>
      <Button variant="outline" disabled={status !== 'MAINTENANCE' || end.isPending} onClick={() => end.mutate()}>
        End maintenance
      </Button>
      <Button variant="destructive" onClick={() => setConfirming(true)}>
        Inject fault
      </Button>
      <Dialog open={confirming} onOpenChange={setConfirming}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Simulate a critical fault on {code}?</DialogTitle>
            <DialogDescription>
              The simulator will raise a critical alarm on this machine, which takes it DOWN until the alarm clears.
            </DialogDescription>
          </DialogHeader>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
            <Button variant="destructive" disabled={inject.isPending} onClick={() => void injectFault()}>
              Simulate fault
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export function EquipmentDetailPage() {
  const { code = '' } = useParams()
  const { user } = useAuth()
  const equipment = useEquipmentDetail(code)
  const now = useNow()

  if (equipment.isError) return <p className="text-sm text-destructive">Could not load equipment {code}.</p>
  if (!equipment.data) return <p className="text-sm text-muted-foreground">Loading...</p>

  const e = equipment.data
  const run = e.openRun

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="flex items-center gap-3 text-2xl font-semibold">
            <span>
              {e.name} <span className="font-mono text-lg text-muted-foreground">{e.code}</span>
            </span>
            <StatusBadge value={e.status} />
          </h1>
          <p className="text-sm text-muted-foreground">
            {run ? (
              <>
                Running <span className="font-mono">{run.workOrderNumber}</span>
                {run.parentLotId && (
                  <>
                    {' '}
                    from <span className="font-mono">{run.parentLotId}</span>
                  </>
                )}
                , started {formatDuration((now - Date.parse(run.startedAt)) / 1000)} ago
              </>
            ) : (
              'No open run'
            )}
          </p>
        </div>
        {user?.role === 'ADMIN' && <AdminActions code={e.code} status={e.status} />}
      </div>

      <Tabs defaultValue="trends">
        <TabsList>
          <TabsTrigger value="trends">Trends</TabsTrigger>
          <TabsTrigger value="status-log">Status log</TabsTrigger>
          <TabsTrigger value="alarms">Alarms</TabsTrigger>
        </TabsList>
        <TabsContent value="trends">
          <TrendsTab code={e.code} />
        </TabsContent>
        <TabsContent value="status-log">
          <StatusLogTab code={e.code} />
        </TabsContent>
        <TabsContent value="alarms">
          <AlarmsTab code={e.code} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
