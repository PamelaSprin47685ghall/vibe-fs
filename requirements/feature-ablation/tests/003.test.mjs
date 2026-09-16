import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const nodesDoc = JSON.parse(read('resources/ablation/nodes.json'))

test('WHAT[ABL-003] ABL_003_station_order_edges_are_acyclic_by_construction', () => {
  const edges = nodesDoc.edges.filter((edge) => edge.kind === 'station-order')
  const graph = new Map()
  for (const edge of edges) {
    if (!graph.has(edge.from)) graph.set(edge.from, [])
    graph.get(edge.from).push(edge.to)
  }
  const visiting = new Set()
  const visited = new Set()
  const visit = (node) => {
    if (visited.has(node)) return
    if (visiting.has(node)) throw new Error(`cycle at ${node}`)
    visiting.add(node)
    for (const next of graph.get(node) ?? []) visit(next)
    visiting.delete(node)
    visited.add(node)
  }
  for (const node of graph.keys()) visit(node)
})
