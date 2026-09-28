import assert from 'node:assert/strict'
import test from 'node:test'
import { formatDiagnostics } from '../../verification-system/tests/e2e/support/diagnostics-format.js'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const flow = (id) => causal.owner('flow', { id })

test('WHAT[causal-wait-007] diagnostic output presents the causal frontier before the event tail', () => {
  const output = formatDiagnostics({
    causalWaitSnapshot: { active: [], history: [] },
    causalFrontier: [{ kind: 'BrokenCausalEdge', detail: 'owner-A waits for missing producer-B', chain: [] }],
    events: [{ seq: 1, time: 'T0', type: 'event-tail-marker' }],
  })
  const frontier = output.indexOf('owner-A waits for missing producer-B')
  const tail = output.indexOf('event-tail-marker')
  assert.ok(frontier >= 0, 'the actual frontier must appear')
  assert.ok(tail > frontier, 'the event tail must follow the causal explanation')
})

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

test('WHAT[causal-wait-007] RED_5_nested_graph_walks_to_external_frontier', () => {
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

test('WHAT[causal-wait-007] RED_6_missing_producer_reports_broken_causal_edge', () => {
  const frontiers = causal.frontiers([waitWorkflow('A', 'B')])

  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'BrokenCausalEdge')
  assert.deepEqual(
    frontiers[0].chain.map((node) => causal.ownerKey(node.owner)),
    ['flow:id=A', 'flow:id=B'],
  )
  assert.match(frontiers[0].detail, /BROKEN CAUSAL EDGE/)
})

test('WHAT[causal-wait-007] RED_7_cycle_reports_without_hanging', () => {
  const frontiers = causal.frontiers([
    waitWorkflow('A', 'B'),
    waitWorkflow('B', 'C'),
    waitWorkflow('C', 'A'),
  ])

  assert.ok(frontiers.length >= 1)
  assert.ok(frontiers.every((frontier) => frontier.kind === 'CausalWaitCycle'))
  const cycle = frontiers[0]
  assert.match(cycle.detail, /CAUSAL WAIT CYCLE/)
  assert.deepEqual(cycle.cycle.map(causal.ownerKey), ['flow:id=A', 'flow:id=B', 'flow:id=C', 'flow:id=A'])
  for (const key of ['flow:id=A', 'flow:id=B', 'flow:id=C']) {
    assert.ok(cycle.chain.some((node) => causal.ownerKey(node.owner) === key), `chain should include ${key}`)
  }
})

test('WHAT[causal-wait-007] a cycle explanation excludes the noncyclic prefix', () => {
  const [frontier] = causal.frontiers([waitWorkflow('root', 'B'), waitWorkflow('B', 'C'), waitWorkflow('C', 'B')])
  assert.equal(frontier.kind, 'CausalWaitCycle')
  assert.deepEqual(frontier.chain.map(node => causal.ownerKey(node.owner)), ['flow:id=root', 'flow:id=B', 'flow:id=C'])
  assert.deepEqual(frontier.cycle.map(causal.ownerKey), ['flow:id=B', 'flow:id=C', 'flow:id=B'])
})

test('WHAT[causal-wait-007] all unsatisfied branches of one active owner appear in the frontier', { todo: 'GAP-094: the current walk chooses only the first wait for each owner' }, () => {
  const frontiers = causal.frontiers([waitExternal('A', 'B'), waitExternal('A', 'C')])
  assert.deepEqual(frontiers.map(frontier => causal.producerKey(frontier.producer)).sort(), ['external:ext:id=B', 'external:ext:id=C'])
})

test('WHAT[causal-wait-007] empty_snapshot_yields_empty_frontier', () => {
  const frontiers = causal.frontiers([])
  assert.equal(frontiers.length, 1)
  assert.equal(frontiers[0].kind, 'Empty')
  assert.deepEqual(frontiers[0].chain, [])
  assert.match(frontiers[0].detail, /no active waits/)
})
