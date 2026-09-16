// CAUSAL-007 — pure frontier explanations over plain diagnostic descriptors.

import assert from 'node:assert/strict'
import test from 'node:test'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const flow = (id) => causal.owner('flow', { id })
const waitWorkflow = (ownerId, producerOwnerId) =>
  causal.createWait({
    waitKind: 'workflow',
    owner: flow(ownerId),
    subject: { producer: producerOwnerId },
    producer: causal.workflowProducer(flow(producerOwnerId)),
    escapes: [causal.escape('processLifetime')],
    source: 'causal-frontier.test',
  })
const waitExternal = (ownerId, externalId) =>
  causal.createWait({
    waitKind: 'external',
    owner: flow(ownerId),
    subject: { producer: externalId },
    producer: causal.externalProducer('ext', { id: externalId }),
    escapes: [causal.escape('processLifetime')],
    source: 'causal-frontier.test',
  })

test('WHAT[CAUSAL-007] RED_5_nested_graph_walks_to_external_frontier', () => {
  const frontiers = causal.frontiers([waitWorkflow('A', 'B'), waitExternal('B', 'C')])

  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'ExternalProducerFrontier')
  assert.deepEqual(
    frontiers[0].chain.map((node) => causal.ownerKey(node.owner)),
    ['flow:id=A', 'flow:id=B'],
  )
  assert.equal(causal.producerKey(frontiers[0].producer), 'external:ext:id=C')
  assert.match(frontiers[0].detail, /FRONTIER: waiting for external producer/)
})

test('WHAT[CAUSAL-007] RED_6_missing_producer_reports_broken_causal_edge', () => {
  const frontiers = causal.frontiers([waitWorkflow('A', 'B')])

  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'BrokenCausalEdge')
  assert.deepEqual(
    frontiers[0].chain.map((node) => causal.ownerKey(node.owner)),
    ['flow:id=A', 'flow:id=B'],
  )
  assert.match(frontiers[0].detail, /BROKEN CAUSAL EDGE/)
})

test('WHAT[CAUSAL-007] RED_7_cycle_reports_without_hanging', () => {
  const frontiers = causal.frontiers([
    waitWorkflow('A', 'B'),
    waitWorkflow('B', 'C'),
    waitWorkflow('C', 'A'),
  ])

  assert.ok(frontiers.length >= 1)
  assert.ok(frontiers.every((frontier) => frontier.kind === 'CausalWaitCycle'))
  const cycle = frontiers[0]
  assert.match(cycle.detail, /CAUSAL WAIT CYCLE/)
  assert.ok(cycle.cycle.length >= 1, 'cycle list must be non-empty')
  for (const key of ['flow:id=A', 'flow:id=B', 'flow:id=C']) {
    assert.ok(cycle.chain.some((node) => causal.ownerKey(node.owner) === key), `chain should include ${key}`)
  }
})

test('WHAT[CAUSAL-007] empty_snapshot_yields_empty_frontier', () => {
  const frontiers = causal.frontiers([])
  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'Empty')
  assert.deepEqual(frontiers[0].chain, [])
  assert.match(frontiers[0].detail, /no active waits/)
})
