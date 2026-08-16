import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`
const frame = () => Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const prepared = (value, decisionId = 'd1', target = 'run-1') => Strength.eventPrepared('owner', decisionId, target, `replica-${decisionId}`, 'K1', 'anchor-a', value.digest, value.byteLength, [`p-${decisionId}`])
const promoted = (value, decisionId = 'd1', target = 'run-1') => Strength.eventPromoted('owner', decisionId, target, value.digest, [`p-${decisionId}`])
const apply = (state, event) => {
  const result = Strength.projectionApply(state, event)
  assert.equal(result.ok, true, result.error)
  return result.value
}
const turn = (providerRun, parts, outcome = 'completed') => ({ sessionId: 'owner', physicalUserMessageId: 'user-1', authorityRootUserMessageId: 'user-1', providerRun, parts, outcome })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })

test('WHAT[SPEC-INV-007] STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output', () => {
  const value = frame()
  let projection = apply(Strength.projectionEmpty(), prepared(value))
  const realTurn = turn('run-1', [call('c1', 'read', '{}')])
  const eventView = Strength.lifecycleReconcileEvent(projection, realTurn)
  assert.equal(eventView.kind, 'Promoted')
  assert.equal(eventView.decisionId, 'd1')
  assert.equal(eventView.frameDigest, value.digest)
  assert.equal(Strength.lifecycleReconcileEvent(projection, { ...realTurn, providerRun: 'other' }), null)
  const abandonedView = Strength.lifecycleReconcileEvent(projection, turn('run-1', [call('partial', 'read', '{}')], 'failed'))
  assert.equal(abandonedView.kind, 'Abandoned')
  projection = apply(projection, Strength.eventAbandoned('d1', 'run-1'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), null)
  projection = apply(apply(Strength.projectionEmpty(), prepared(value)), Strength.eventPromoted('owner', 'd1', 'run-1', value.digest, ['p-d1']))
  assert.equal(Strength.lifecycleReconcileEvent(projection, realTurn), null)
})

test('WHAT[SPEC-INV-008] STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor', async () => {
  const value = frame()
  let projection = apply(Strength.projectionEmpty(), prepared(value))
  const messages = [{ id: 'user-1' }, { id: 'run-1' }, { id: 'user-2' }]
  let replay = await Strength.lifecycleReplayPlans('owner', messages, value, projection)
  assert.equal(replay.ok, true)
  assert.equal(replay.value.length, 0)
  projection = apply(projection, promoted(value))
  replay = await Strength.lifecycleReplayPlans('owner', messages, value, projection)
  assert.equal(replay.ok, true)
  const [plan] = replay.value
  assert.equal(plan.beforeMessageIndex, 1)
  assert.equal(plan.bundle.digest, value.digest)
  assert.equal(plan.existingTraceRange, null)
  projection = apply(projection, Strength.eventTraced('d1', 10n, 14n))
  const traced = (await Strength.lifecycleReplayPlans('owner', messages, value, projection)).value[0]
  assert.equal(Strength.lifecycleNeedsRawReplay(12n, traced), true)
  assert.equal(Strength.lifecycleNeedsRawReplay(13n, traced), false)
  assert.equal(Strength.lifecycleNeedsRawReplay(20n, traced), false)
  const missing = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }], value, projection)
  assert.equal(missing.ok, false)
  assert.match(missing.error, /target anchor is absent/i)
})

test('WHAT[SPEC-INV-006] STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed', async () => {
  const value = frame()
  const projection = apply(Strength.projectionEmpty(), prepared(value))
  assert.equal(Strength.projectionIsPromoted('d1', projection), false)
  const traced = Strength.projectionApply(projection, Strength.eventTraced('d1', 10n, 14n))
  assert.equal(traced.ok, false)
  const replay = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)
  assert.equal(replay.ok, true)
  assert.equal(replay.value.length, 0)
})

test('WHAT[SPEC-INV-008] STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage', async () => {
  const value = frame()
  let projection = apply(Strength.projectionEmpty(), prepared(value))
  projection = apply(projection, promoted(value))
  projection = apply(projection, Strength.eventTraced('d1', 40n, 44n))
  const plan = (await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)).value[0]
  assert.equal(Strength.lifecycleNeedsRawReplay(null, plan), true)
  assert.equal(Strength.lifecycleNeedsRawReplay(42n, plan), true)
})

test('WHAT[SPEC-INV-008] STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match', () => {
  const value = frame()
  const expected = Strength.traceExpectedParts(value)
  assert.equal(expected.length, 4)
  const observed = expected.map((part, index) => ({ cursorSequence: 20n + BigInt(index), kind: part.kind, toolName: part.toolName, body: part.body }))
  const recovered = Strength.traceRecoverRange(value, observed)
  assert.equal(recovered.ok, true)
  assert.equal(recovered.value.startInclusive, 20n)
  assert.equal(recovered.value.endExclusive, 24n)
  const ambiguous = Strength.traceRecoverRange(value, [...observed, ...observed.map((part, index) => ({ ...part, cursorSequence: 30n + BigInt(index) }))])
  assert.equal(ambiguous.ok, false)
  assert.match(ambiguous.error, /ambiguous/i)
  const gapped = observed.map((part, index) => ({ ...part, cursorSequence: index < 2 ? part.cursorSequence : part.cursorSequence + 1n }))
  const gapResult = Strength.traceRecoverRange(value, gapped)
  assert.equal(gapResult.ok, false)
  assert.match(gapResult.error, /contiguous/i)
})
