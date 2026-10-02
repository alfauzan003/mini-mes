import { describe, expect, it } from 'vitest'
import type { GenealogyGraph, LotDto } from '@/shared/api/types'
import { layoutGenealogy } from './genealogyLayout'

function lot(lotId: string): LotDto {
  return {
    lotId,
    type: 'RAW',
    polarity: 'CATHODE',
    productCode: null,
    materialCode: null,
    workOrderNumber: null,
    qty: 1,
    uom: 'kg',
    status: 'WAIT',
    quality: 'NONE',
    currentOperation: null,
    nextOperation: null,
    currentEquipment: null,
    currentCarrier: null,
    createdAt: '2026-10-03T00:00:00Z',
  }
}

// PANCAKE <- ELECTRODE <- (FOIL, SLURRY); SLURRY <- (RAW-A, RAW-B)
const backward: GenealogyGraph = {
  rootLotId: 'PAN',
  nodes: ['PAN', 'ELE', 'FOIL', 'SLU', 'RAW-B', 'RAW-A'].map(lot),
  edges: [
    { parentLotId: 'ELE', childLotId: 'PAN' },
    { parentLotId: 'FOIL', childLotId: 'ELE' },
    { parentLotId: 'SLU', childLotId: 'ELE' },
    { parentLotId: 'RAW-A', childLotId: 'SLU' },
    { parentLotId: 'RAW-B', childLotId: 'SLU' },
  ],
}

// FOIL -> (PAN-1, PAN-2); PAN-1 -> CUT
const forward: GenealogyGraph = {
  rootLotId: 'FOIL',
  nodes: ['FOIL', 'PAN-2', 'PAN-1', 'CUT'].map(lot),
  edges: [
    { parentLotId: 'FOIL', childLotId: 'PAN-1' },
    { parentLotId: 'FOIL', childLotId: 'PAN-2' },
    { parentLotId: 'PAN-1', childLotId: 'CUT' },
  ],
}

const xOf = (layout: ReturnType<typeof layoutGenealogy>, id: string) =>
  layout.nodes.find((n) => n.id === id)?.position.x

describe('layoutGenealogy', () => {
  it('places root at depth 0 and ancestors to the left when backward', () => {
    const layout = layoutGenealogy(backward, 'backward')

    expect(xOf(layout, 'PAN')).toBe(0)
    expect(xOf(layout, 'ELE')).toBe(-260)
    expect(xOf(layout, 'FOIL')).toBe(-520)
    expect(xOf(layout, 'SLU')).toBe(-520)
    expect(xOf(layout, 'RAW-A')).toBe(-780)
  })

  it('places descendants to the right when forward', () => {
    const layout = layoutGenealogy(forward, 'forward')

    expect(xOf(layout, 'FOIL')).toBe(0)
    expect(xOf(layout, 'PAN-1')).toBe(260)
    expect(xOf(layout, 'PAN-2')).toBe(260)
    expect(xOf(layout, 'CUT')).toBe(520)
  })

  it('stacks same-depth nodes with distinct y sorted by lot id', () => {
    const layout = layoutGenealogy(backward, 'backward')
    const raws = layout.nodes.filter((n) => n.id.startsWith('RAW'))
    const middle = layout.nodes.filter((n) => n.position.x === -520)

    expect(raws.map((n) => n.id)).toEqual(['RAW-A', 'RAW-B'])
    expect(raws.map((n) => n.position.y)).toEqual([-45, 45])
    expect(middle.map((n) => n.id)).toEqual(['FOIL', 'SLU'])
    expect(layout.nodes.find((n) => n.id === 'PAN')?.position.y).toBe(0)
  })

  it('edges go parent to child', () => {
    const layout = layoutGenealogy(backward, 'backward')

    expect(layout.edges).toContainEqual({ id: 'FOIL->ELE', source: 'FOIL', target: 'ELE' })
    expect(layout.edges).toContainEqual({ id: 'ELE->PAN', source: 'ELE', target: 'PAN' })
    expect(layout.edges).toHaveLength(5)
  })

  it('carries the lot as node data', () => {
    const layout = layoutGenealogy(forward, 'forward')

    expect(layout.nodes.find((n) => n.id === 'CUT')?.data.lotId).toBe('CUT')
  })
})
