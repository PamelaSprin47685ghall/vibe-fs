import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-002] canonical parser normalizes only newline shape and preserves lane text', () => {
  const parsed = mustOk(fission.parsePrompt('  A  \r\nB\r\n'))
  assert.deepEqual(
    parsed.lanes.map((lane) => [lane.index, lane.prompt]),
    [
      [0, '  A  '],
      [1, 'B'],
    ],
  )
  assert.equal(parsed.count, 2)

  const internalBlank = fission.parsePrompt('A\n   \nC')
  assert.equal(internalBlank.ok, false)
  assert.equal(internalBlank.reason, 'EmptyLanePrompt')
  assert.equal(internalBlank.laneIndex, 1)

  const tooFew = fission.parsePrompt('A')
  assert.equal(tooFew.ok, false)
  assert.equal(tooFew.reason, 'TooFewLanes')
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-006] pre-fission completion broadcasts to every lane exactly once with idempotent delivery', () => {
  assert.deepEqual(fission.completionTargets(4, { kind: 'pre-fission' }), [0, 1, 2, 3])

  let delivery = fission.deliveryEmpty(3)
  delivery = mustOk(fission.deliveryMark('child-A', 0, delivery)).delivery
  delivery = mustOk(fission.deliveryMark('child-A', 0, delivery)).delivery // idempotent
  delivery = mustOk(fission.deliveryMark('child-A', 2, delivery)).delivery
  assert.deepEqual(fission.deliveryPendingTargets('child-A', delivery), [1])
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-007] post-fission completion has exactly one affinity target: the initiating lane', () => {
  assert.deepEqual(fission.completionTargets(4, { kind: 'lane', index: 2 }), [2])
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-008] keyed work bundle is idempotent and rejects conflicting records for one lane', () => {
  const empty = fission.workBundleEmpty
  const a = mustOk(fission.workBundleAdd(2, 'ref-c', empty)).bundle
  const b = mustOk(fission.workBundleAdd(0, 'ref-a', a)).bundle
  const same = mustOk(fission.workBundleAdd(0, 'ref-a', b)).bundle
  assert.deepEqual(fission.workBundleKeys(same), [0, 2])

  const conflict = fission.workBundleAdd(0, 'ref-other', same)
  assert.equal(conflict.ok, false)
  assert.equal(conflict.reason, 'ConflictingLaneRecord')

  const left = mustOk(fission.workBundleAdd(1, 'ref-b', b)).bundle
  const right = mustOk(fission.workBundleAdd(1, 'ref-b', mustOk(fission.workBundleAdd(0, 'ref-a', empty)).bundle)).bundle
  const merged1 = mustOk(fission.workBundleMerge(left, right)).bundle
  const merged2 = mustOk(fission.workBundleMerge(right, left)).bundle
  assert.deepEqual(fission.workBundleEntries(merged1), fission.workBundleEntries(merged2))
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] convergence requires all lane records and all completion deliveries', () => {
  const bundle = [0, 1, 2].reduce(
    (state, lane) => mustOk(fission.workBundleAdd(lane, `ref-${lane}`, state)).bundle,
    fission.workBundleEmpty,
  )
  let delivery = fission.deliveryEmpty(3)
  for (const lane of [0, 1, 2]) delivery = mustOk(fission.deliveryMark('pre-child', lane, delivery)).delivery

  assert.equal(fission.convergenceReady(3, ['pre-child'], bundle, delivery), true)
  const incomplete = mustOk(fission.workBundleAdd(0, 'ref-0', fission.workBundleEmpty)).bundle
  assert.equal(fission.convergenceReady(3, ['pre-child'], incomplete, delivery), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-001] lanes carry no provider-visible identity or handle and keep the same logical participant', () => {
  const lane = fission.startedLane(1, 'lane-session-1', 'lane input')
  assert.equal(lane.index, 1)
  assert.equal(lane.prompt, 'lane input')
  assert.equal(lane.hasAgentId, false, 'lane record must not expose a provider-visible AgentId')
  assert.equal(lane.hasHandle, false, 'lane record must not expose a provider-visible handle')
  assert.equal(lane.hasParent, false, 'lane record must not add a parent join obligation of its own')

  const startup = fission.startup(2, 0, 'lane A', 'CANONICAL-LWR')
  assert.match(startup, /same logical participant/, 'startup keeps the lanes under one logical identity')
  assert.match(startup, /Do not treat sibling lanes as delegated agents/, 'startup must not turn lanes into new delegation identities')
})
