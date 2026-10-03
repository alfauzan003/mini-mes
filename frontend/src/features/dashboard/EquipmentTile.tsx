import { Bell } from 'lucide-react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import type { EquipmentDto, EquipmentStatus, LiveReadingDto } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { LiveParameters } from './LiveParameters'

const BORDER_BY_STATUS: Record<EquipmentStatus, string> = {
  RUNNING: 'border-l-blue-500',
  DOWN: 'border-l-red-500',
  MAINTENANCE: 'border-l-amber-500',
  IDLE: 'border-l-slate-300',
}

interface EquipmentTileProps {
  equipment: EquipmentDto
  readings: LiveReadingDto[]
  activeAlarmCount: number
}

export function EquipmentTile({ equipment, readings, activeAlarmCount }: EquipmentTileProps) {
  const run = equipment.openRun
  return (
    <Link
      to={`/equipment/${equipment.code}`}
      className={cn(
        'block rounded-xl border border-l-4 bg-card p-4 outline-none transition-colors hover:bg-muted/50 focus-visible:ring-3 focus-visible:ring-ring/50',
        BORDER_BY_STATUS[equipment.status],
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-lg font-semibold">{equipment.code}</span>
        <StatusBadge value={equipment.status} />
      </div>
      <p className="text-sm text-muted-foreground">{equipment.name}</p>
      <div className="mt-2 text-sm">
        {run ? (
          <>
            <span className="font-mono">{run.workOrderNumber}</span>
            {run.parentLotId && (
              <>
                {' '}
                <span className="text-muted-foreground">from</span> <span className="font-mono">{run.parentLotId}</span>
              </>
            )}
          </>
        ) : (
          <span className="text-muted-foreground">No run</span>
        )}
      </div>
      <div className="mt-2">
        <LiveParameters readings={readings} status={equipment.status} />
      </div>
      {activeAlarmCount > 0 && (
        <Badge variant="outline" className="mt-2 border-red-200 bg-red-100 text-red-800">
          <Bell aria-hidden="true" />
          {activeAlarmCount} {activeAlarmCount === 1 ? 'alarm' : 'alarms'}
        </Badge>
      )}
    </Link>
  )
}
