import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import {
  store, persistence, digest, body, envelope, graphNode, work, transition, batch, append, current, mustOk, withStore,
} from './persistence-support.mjs'
import {
  interpretationInquiry, rawObservation, interpretationPending,
  interpretationApplied, interpretationFailed, interpretationSeedBodies,
  pendingInterpretationRecord, appliedInterpretationRecord, failedInterpretationRecord,
  interpretationRecord, expectInterpretationRefusal, journalBytes,
} from './interpretation-support.mjs'

test('WHAT[sphinx-v2-021] recovery classifies accepted observations for interpretation without buying another response', () => {
  assert.equal(Loop.recoveryAction('ResultPending'), 'accept-if-valid')
  assert.equal(Loop.recoveryAction('InterpretationPending'), 'interpret')
  assert.equal(Loop.recoveryMaySpend('InterpretationPending'), false)
})

test.todo('WHAT[sphinx-v2-021] actual two-transaction recovery preserves accepted raw responses and retries only pure interpretation after plugin failure')

test('WHAT[sphinx-v2-021] a second durable transaction records Applied and preserves the accepted raw response through cold replay', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const accepted = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
    const pending = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(pending), pendingInterpretationRecord)
    assert.equal(Object.keys(interpretationRecord(pending)).length, 7)
    const graph = body('GraphPatched', {
      pluginRef: interpretationApplied.payload.pluginRef,
      patch: { upsertNodes: [graphNode('derived-observation')], removeNodes: [], upsertEdges: [], removeEdges: [] },
    })
    const applied = await append(writer, batch(interpretationInquiry, 'pure-observe', [graph, interpretationApplied], accepted))
    const live = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(live), appliedInterpretationRecord)
    assert.deepEqual(mustOk(live).observations, mustOk(pending).observations)
    assert.equal(mustOk(live).observations[0].value.canonicalResult, rawObservation.payload.canonicalResult)
    assert.deepEqual(mustOk(live).work, mustOk(pending).work)
    assert.deepEqual(mustOk(live).reservations, mustOk(pending).reservations)
    assert.deepEqual(mustOk(live).settledUsage, mustOk(pending).settledUsage)
    assert.deepEqual(mustOk(live).graph.map(entry => entry.key), ['derived-observation'])
    assert.notEqual(live.stateHash, pending.stateHash)
    assert.deepEqual(store.read(writer, applied.id).payload.events, [graph, interpretationApplied])
    close(writer)
    assert.deepEqual(current(open(), interpretationInquiry), live)
  })
})

test('WHAT[sphinx-v2-021] a durable Failed outcome stores its exact reason without losing the response or reopening the paid work', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const accepted = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
    const pending = current(writer, interpretationInquiry)
    const failed = await append(writer, batch(interpretationInquiry, 'failed-observe', [interpretationFailed], accepted))
    const live = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(live), failedInterpretationRecord)
    assert.equal(Object.keys(interpretationRecord(live)).length, 7)
    assert.deepEqual(mustOk(live).observations, mustOk(pending).observations)
    assert.deepEqual(mustOk(live).work, mustOk(pending).work)
    assert.deepEqual(mustOk(live).reservations, mustOk(pending).reservations)
    assert.deepEqual(mustOk(live).status, { case: 'Active' })
    assert.deepEqual(store.read(writer, failed.id).payload.events, [interpretationFailed])
    close(writer)
    assert.deepEqual(current(open(), interpretationInquiry), live)
  })
})

for (const [name, outcome, expected] of [
  ['Applied', interpretationApplied, appliedInterpretationRecord],
  ['Failed', interpretationFailed, failedInterpretationRecord],
]) {
  test('WHAT[sphinx-v2-021] exact ' + name + ' and Pending replays retain the original terminal interpretation', async () => {
    await withStore(async ({ open, close }) => {
      const writer = open()
      const accepted = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
      const terminal = await append(writer, batch(interpretationInquiry, 'original-outcome', [outcome], accepted))
      const before = mustOk(current(writer, interpretationInquiry))
      const replay = await append(writer, batch(interpretationInquiry, 'exact-outcome-replay', [
        interpretationPending, outcome, interpretationPending, outcome,
      ], terminal))
      const live = current(writer, interpretationInquiry)
      assert.deepEqual(interpretationRecord(live), expected)
      assert.deepEqual(mustOk(live).observations, before.observations)
      assert.deepEqual(mustOk(live).work, before.work)
      assert.equal(mustOk(live).revision, '2', 'a new exact-replay command advances the envelope, not its recorded outcome')
      assert.equal(mustOk(live).eventHead, replay.id)
      close(writer)
      assert.deepEqual(current(open(), interpretationInquiry), live)
    })
  })
}

test('WHAT[sphinx-v2-021] changed or opposite outcomes refuse their whole batch without replacing the original interpretation', async t => {
  const changed = [
    { name: 'Applied delta', original: interpretationApplied, candidate: body('InterpretationApplied', { ...interpretationApplied.payload, delta: envelope('{"interpretation":"改写"}') }) },
    { name: 'Applied schema identity', original: interpretationApplied, candidate: body('InterpretationApplied', { ...interpretationApplied.payload, delta: { ...interpretationApplied.payload.delta, schema: { ...interpretationApplied.payload.delta.schema, id: 'other@2' } } }) },
    { name: 'Applied schema content hash', original: interpretationApplied, candidate: body('InterpretationApplied', { ...interpretationApplied.payload, delta: { ...interpretationApplied.payload.delta, schema: { ...interpretationApplied.payload.delta.schema, hash: digest('{"type":"string"}') } } }) },
    { name: 'Applied interpretation identity', original: interpretationApplied, candidate: body('InterpretationApplied', { ...interpretationApplied.payload, interpretationId: 'new-interpretation' }) },
    { name: 'Applied plugin implementation reference', original: interpretationApplied, candidate: body('InterpretationApplied', { ...interpretationApplied.payload, pluginRef: 'proof-plugin@repaired' }) },
    { name: 'Applied to Failed', original: interpretationApplied, candidate: interpretationFailed },
    { name: 'Failed reason', original: interpretationFailed, candidate: body('InterpretationFailed', { ...interpretationFailed.payload, reason: 'different failure' }) },
    { name: 'Failed interpretation identity', original: interpretationFailed, candidate: body('InterpretationFailed', { ...interpretationFailed.payload, interpretationId: 'new-interpretation' }) },
    { name: 'Failed plugin implementation reference', original: interpretationFailed, candidate: body('InterpretationFailed', { ...interpretationFailed.payload, pluginRef: 'proof-plugin@repaired' }) },
    { name: 'Failed to Applied', original: interpretationFailed, candidate: interpretationApplied },
  ]
  for (const scenario of changed) {
    await t.test('WHAT[sphinx-v2-021] refuses changed ' + scenario.name + ' atomically', async () => {
      await withStore(async ({ open, close, commonDir }) => {
        const writer = open()
        const accepted = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
        const terminal = await append(writer, batch(interpretationInquiry, 'original-outcome', [scenario.original], accepted))
        const before = current(writer, interpretationInquiry)
        expectInterpretationRefusal(writer, terminal, [
          body('GraphPatched', {
            pluginRef: 'proof-plugin@original',
            patch: { upsertNodes: [graphNode('must-not-publish')], removeNodes: [], upsertEdges: [], removeEdges: [] },
          }),
          interpretationPending, scenario.candidate,
        ], commonDir)
        assert.deepEqual(mustOk(current(writer, interpretationInquiry)).graph, [])
        close(writer)
        assert.deepEqual(current(open(), interpretationInquiry), before)
      })
    })
  }
})

test('WHAT[sphinx-v2-021] the actual driver leaves pending interpretation for a real Observe owner and does not dispatch another Ready work', async () => {
  await withStore(async ({ open, commonDir }) => {
    const writer = open()
    const pending = await append(writer, batch(interpretationInquiry, 'pending-and-ready', [
      ...interpretationSeedBodies(),
      body('WorkPlanned', { work: [{ ...work('ready-after-observation'), roundId: null }] }),
      transition('ready-after-observation', 'Planned', { case: 'Ready' }),
    ]))
    const before = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(before), pendingInterpretationRecord)
    assert.equal(mustOk(before).work.find(entry => entry.key === 'ready-after-observation').value.state.case, 'Ready')
    const bytes = journalBytes(commonDir)
    const plan = mustOk(persistence.proposeAdvance(writer, digest, interpretationInquiry))
    assert.deepEqual(plan.outcome, { Outcome: 'refinement-pending', Detail: '1' })
    assert.deepEqual(plan.events, [], 'without a bound Observe the driver must not fabricate Applied or buy more work')
    assert.deepEqual(current(writer, interpretationInquiry), before)
    assert.deepEqual(journalBytes(commonDir), bytes)

    await append(writer, batch(interpretationInquiry, 'recorded-observe', [interpretationApplied], pending))
    const settled = current(writer, interpretationInquiry)
    const settledBytes = journalBytes(commonDir)
    const readyPlan = mustOk(persistence.proposeAdvance(writer, digest, interpretationInquiry))
    assert.deepEqual(readyPlan.outcome, { Outcome: 'awaiting-results', Detail: '1' })
    const requests = readyPlan.events.filter(event => event.case === 'DispatchRequested')
    assert.equal(requests.length, 1, 'the original Ready proposal becomes available after recorded interpretation')
    assert.equal(requests[0].payload.work.id, 'ready-after-observation')
    assert.deepEqual(current(writer, interpretationInquiry), settled)
    assert.deepEqual(journalBytes(commonDir), settledBytes)
  })
})
