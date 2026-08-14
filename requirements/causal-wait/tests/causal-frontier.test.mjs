// DSL-012 causal frontier algorithm (RED-5..RED-7 + empty snapshot).
// Pure diagnostic explanation of the first unsatisfied consumer→producer edge.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, causalWait, listItems } from '../../../tests/unit/support/domain.mjs'

const flow = (id) => causalWait.owner('flow', [['id', id]])

const waitWorkflow = (ownerId, producerOwnerId) =>
  causalWait.create({
    waitKind: 'workflow',
    owner: flow(ownerId),
    subject: [['producer', producerOwnerId]],
    producer: causalWait.workflowProducer(flow(producerOwnerId)),
    source: 'causal-frontier.test',
  })

const waitExternal = (ownerId, externalId) =>
  causalWait.create({
    waitKind: 'external',
    owner: flow(ownerId),
    subject: [['producer', externalId]],
    producer: causalWait.externalProducer('ext', [['id', externalId]]),
    source: 'causal-frontier.test',
  })

const viewFrontiers = (active) => {
  const snapshot = causalWait.snapshotOf({ active, history: [], sequence: 1n })
  return causalWait.frontiersOf(snapshot).map((frontier) => ({
    kind: caseOf(frontier.Kind),
    chainKeys: listItems(frontier.Chain).map((node) => causalWait.ownerKey(node.Owner)),
    cycleKeys: listItems(frontier.Cycle).map((owner) => causalWait.ownerKey(owner)),
    producerKey:
      frontier.FrontierProducer == null ? undefined : causalWait.producerKey(frontier.FrontierProducer),
    detail: frontier.Detail,
  }))
}

test('RED_5_nested_graph_walks_to_external_frontier', () => {
  // A waits B; B waits External C  →  A → B → C
  const frontiers = viewFrontiers([waitWorkflow('A', 'B'), waitExternal('B', 'C')])

  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'ExternalProducerFrontier')
  assert.deepEqual(frontiers[0].chainKeys, ['flow:id=A', 'flow:id=B'])
  assert.equal(frontiers[0].producerKey, 'external:ext:id=C')
  assert.match(frontiers[0].detail, /FRONTIER: waiting for external producer/)
})

test('RED_6_missing_producer_reports_broken_causal_edge', () => {
  // A waits B; B has no active wait
  const frontiers = viewFrontiers([waitWorkflow('A', 'B')])

  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'BrokenCausalEdge')
  assert.deepEqual(frontiers[0].chainKeys, ['flow:id=A', 'flow:id=B'])
  assert.match(frontiers[0].detail, /BROKEN CAUSAL EDGE/)
})

test('RED_7_cycle_reports_without_hanging', () => {
  // A → B → C → A
  const frontiers = viewFrontiers([
    waitWorkflow('A', 'B'),
    waitWorkflow('B', 'C'),
    waitWorkflow('C', 'A'),
  ])

  assert.ok(frontiers.length >= 1)
  assert.ok(frontiers.every((f) => f.kind === 'CausalWaitCycle'))
  const cycle = frontiers[0]
  assert.match(cycle.detail, /CAUSAL WAIT CYCLE/)
  assert.ok(cycle.cycleKeys.length >= 1, 'cycle list must be non-empty')
  // Walk chain carries the full loop path; Cycle marks the re-entry.
  for (const key of ['flow:id=A', 'flow:id=B', 'flow:id=C']) {
    assert.ok(cycle.chainKeys.includes(key), `chain should include ${key}`)
  }
})

test('empty_snapshot_yields_empty_frontier', () => {
  const frontiers = viewFrontiers([])
  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'Empty')
  assert.deepEqual(frontiers[0].chainKeys, [])
  assert.match(frontiers[0].detail, /no active waits/)
})
