import type { GenealogyGraph, LotDto } from '@/shared/api/types'

export type GenealogyDirection = 'backward' | 'forward'

export interface GenealogyLayoutNode {
  id: string
  position: { x: number; y: number }
  data: LotDto
}

export interface GenealogyLayoutEdge {
  id: string
  source: string
  target: string
}

const COLUMN_WIDTH = 260
const ROW_HEIGHT = 90

/**
 * Lays the graph out in columns by hop distance from the root: ancestors to the left when going backward,
 * descendants to the right when going forward. Edges always point parent to child.
 */
export function layoutGenealogy(
  graph: GenealogyGraph,
  direction: GenealogyDirection,
): { nodes: GenealogyLayoutNode[]; edges: GenealogyLayoutEdge[] } {
  // Walk from the root away from it: backward follows child -> parent, forward follows parent -> child.
  const next = new Map<string, string[]>()
  for (const { parentLotId, childLotId } of graph.edges) {
    const [from, to] = direction === 'backward' ? [childLotId, parentLotId] : [parentLotId, childLotId]
    next.set(from, [...(next.get(from) ?? []), to])
  }

  const depthOf = new Map<string, number>([[graph.rootLotId, 0]])
  const queue = [graph.rootLotId]
  for (let head = 0; head < queue.length; head++) {
    const id = queue[head]
    for (const neighbour of next.get(id) ?? []) {
      if (!depthOf.has(neighbour)) {
        depthOf.set(neighbour, depthOf.get(id)! + 1)
        queue.push(neighbour)
      }
    }
  }

  // Defensive: a node the walk cannot reach still gets drawn, one column beyond the deepest reached one.
  const orphanDepth = Math.max(...depthOf.values()) + 1
  const columns = new Map<number, LotDto[]>()
  for (const node of graph.nodes) {
    const depth = depthOf.get(node.lotId) ?? orphanDepth
    columns.set(depth, [...(columns.get(depth) ?? []), node])
  }

  const nodes: GenealogyLayoutNode[] = []
  for (const [depth, members] of columns) {
    members.sort((a, b) => (a.lotId < b.lotId ? -1 : a.lotId > b.lotId ? 1 : 0))
    const x = depth === 0 ? 0 : (direction === 'backward' ? -depth : depth) * COLUMN_WIDTH
    members.forEach((lot, index) => {
      nodes.push({
        id: lot.lotId,
        position: { x, y: (index - (members.length - 1) / 2) * ROW_HEIGHT },
        data: lot,
      })
    })
  }

  const edges = graph.edges.map(({ parentLotId, childLotId }) => ({
    id: `${parentLotId}->${childLotId}`,
    source: parentLotId,
    target: childLotId,
  }))

  return { nodes, edges }
}
