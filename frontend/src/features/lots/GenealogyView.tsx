import '@xyflow/react/dist/style.css'
import { Handle, MarkerType, Position, ReactFlow, type Node, type NodeProps, type NodeTypes } from '@xyflow/react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { LotDto } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useGenealogy } from './api'
import { layoutGenealogy, type GenealogyDirection } from './genealogyLayout'

type LotNodeData = { lot: LotDto; isRoot: boolean }
type LotFlowNode = Node<LotNodeData, 'lot'>

function LotNode({ data }: NodeProps<LotFlowNode>) {
  const { lot, isRoot } = data
  return (
    <div
      className={cn(
        'w-52 cursor-pointer rounded-lg border bg-card px-3 py-2 text-card-foreground shadow-sm hover:border-ring',
        isRoot && 'border-primary ring-2 ring-primary',
      )}
    >
      <Handle type="target" position={Position.Left} isConnectable={false} />
      <div className="font-mono text-xs font-medium">{lot.lotId}</div>
      <div className="mt-1 flex items-center justify-between gap-2 text-xs text-muted-foreground">
        <span>{lot.type}</span>
        <span className="tabular-nums">
          {lot.qty} {lot.uom}
        </span>
      </div>
      <div className="mt-1.5">
        <StatusBadge value={lot.status} />
      </div>
      <Handle type="source" position={Position.Right} isConnectable={false} />
    </div>
  )
}

const nodeTypes: NodeTypes = { lot: LotNode }
const defaultEdgeOptions = { markerEnd: { type: MarkerType.ArrowClosed } }

const DIRECTIONS: { value: GenealogyDirection; label: string }[] = [
  { value: 'backward', label: 'Backward' },
  { value: 'forward', label: 'Forward' },
]

/** Ancestors (backward) or descendants (forward) of one lot as a left-to-right graph; click a node to open it. */
export function GenealogyView({ lotId }: { lotId: string }) {
  const navigate = useNavigate()
  const [direction, setDirection] = useState<GenealogyDirection>('backward')
  const genealogy = useGenealogy(lotId, direction)

  const graph = useMemo(() => {
    if (!genealogy.data) return null
    const layout = layoutGenealogy(genealogy.data, direction)
    const nodes: LotFlowNode[] = layout.nodes.map((node) => ({
      id: node.id,
      type: 'lot',
      position: node.position,
      data: { lot: node.data, isRoot: node.id === genealogy.data.rootLotId },
    }))
    return { nodes, edges: layout.edges }
  }, [genealogy.data, direction])

  return (
    <div className="space-y-3">
      <div className="flex gap-2" role="group" aria-label="Genealogy direction">
        {DIRECTIONS.map((d) => (
          <Button
            key={d.value}
            size="sm"
            variant={direction === d.value ? 'default' : 'outline'}
            aria-pressed={direction === d.value}
            onClick={() => setDirection(d.value)}
          >
            {d.label}
          </Button>
        ))}
      </div>

      {genealogy.isError && <p className="text-sm text-destructive">Could not load genealogy.</p>}
      {genealogy.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
      {graph && (
        <>
          <div className="h-[28rem] rounded-lg border">
            <ReactFlow
              key={`${lotId}-${direction}`}
              nodes={graph.nodes}
              edges={graph.edges}
              nodeTypes={nodeTypes}
              defaultEdgeOptions={defaultEdgeOptions}
              nodesDraggable={false}
              nodesConnectable={false}
              fitView
              onNodeClick={(_, node) => navigate(`/lots/${node.id}`)}
            />
          </div>
          {graph.nodes.length === 1 && (
            <p className="text-sm text-muted-foreground">
              {direction === 'backward' ? 'This lot has no ancestors.' : 'Nothing has been made from this lot yet.'}
            </p>
          )}
        </>
      )}
    </div>
  )
}
