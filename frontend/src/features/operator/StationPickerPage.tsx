import { Link } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useEquipment } from '@/features/work-orders/api'
import type { EquipmentDto, OperationCode } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/StatusBadge'

const OPERATIONS: { code: OperationCode; label: string }[] = [
  { code: 'MIX', label: 'Mixing' },
  { code: 'COAT', label: 'Coating' },
  { code: 'CAL', label: 'Calendering' },
  { code: 'SLIT', label: 'Slitting' },
]

function EquipmentCard({ equipment }: { equipment: EquipmentDto }) {
  return (
    <Link
      to={`/station/${equipment.code}`}
      className="block rounded-xl outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
    >
      <Card className="min-h-28 transition-colors hover:bg-muted/50">
        <CardHeader>
          <div className="flex items-center justify-between gap-2">
            <CardTitle className="font-mono text-xl">{equipment.code}</CardTitle>
            <StatusBadge value={equipment.status} />
          </div>
        </CardHeader>
        <CardContent className="space-y-1 text-sm">
          <p>{equipment.name}</p>
          <p className="text-muted-foreground">
            Current WO: <span className="font-mono">{equipment.openRun?.workOrderNumber ?? 'none'}</span>
          </p>
        </CardContent>
      </Card>
    </Link>
  )
}

export function StationPickerPage() {
  const equipment = useEquipment()

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Operator Station</h1>
      <p className="text-muted-foreground">Pick the equipment you are working at.</p>

      {equipment.isError && <p className="text-sm text-destructive">Could not load equipment.</p>}
      {equipment.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}

      {OPERATIONS.map(({ code, label }) => {
        const items = equipment.data?.filter((e) => e.operation === code) ?? []
        if (items.length === 0) return null
        return (
          <section key={code} aria-labelledby={`group-${code}`} className="space-y-3">
            <h2 id={`group-${code}`} className="text-lg font-semibold">
              {label}
            </h2>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {items.map((e) => (
                <EquipmentCard key={e.code} equipment={e} />
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}
