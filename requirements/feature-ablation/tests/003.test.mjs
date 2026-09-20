import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const nodesDoc = JSON.parse(read('resources/ablation/nodes.json'))

test('WHAT[feature-ablation-003] ABL_003_station_order_edges_are_acyclic_by_construction', () => {
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

test('WHAT[feature-ablation-003] ABL_003_dag_station_order_and_borrow_edges_are_acyclic', () => {
  const edges = nodesDoc.edges.filter((e) => e.kind === 'station-order' || e.kind === 'borrow')
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

  // Only station-order and borrow edge kinds should exist
  for (const edge of nodesDoc.edges) {
    assert.ok(
      edge.kind === 'station-order' || edge.kind === 'borrow',
      `unexpected edge kind ${edge.kind}`,
    )
  }
})
