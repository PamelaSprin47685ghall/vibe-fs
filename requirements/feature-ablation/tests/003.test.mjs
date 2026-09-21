import assert from 'node:assert/strict'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'

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

test('WHAT[feature-ablation-003] ABL_003_cyclic_graph_load_fails_closed', () => {
  const nodesPath = join(ROOT, 'resources/ablation/nodes.json')
  const original = readFileSync(nodesPath, 'utf8')
  try {
    const doc = JSON.parse(original)
    // requirement-system -> verification-system 已存在，添加反向边形成环
    doc.edges.push({
      from: 'verification-system',
      to: 'requirement-system',
      kind: 'station-order',
    })
    writeFileSync(nodesPath, JSON.stringify(doc, null, 2), 'utf8')

    const result = Ablation.load()
    assert.equal(result.ok, false, 'manifest loading must fail when graph contains cycles')
    assert.equal(result.kind, 'DagViolation')
    assert.match(result.error, /cycle/i)
    assert.deepEqual(Ablation.manifestNodeIds(), [])
  } finally {
    writeFileSync(nodesPath, original, 'utf8')
    Ablation.load()
  }
})
