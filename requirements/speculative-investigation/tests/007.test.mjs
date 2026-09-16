import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const H = (text) => `H(${text})`
const frame = () => Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const prepared = (valueOrOptions = {}, decisionId = 'd1', target = 'run-1') => {
  if (valueOrOptions && typeof valueOrOptions === 'object' && 'byteLength' in valueOrOptions && 'digest' in valueOrOptions) {
    return Strength.eventPrepared('owner', decisionId, target, `replica-${decisionId}`, 'K1', 'anchor-a', valueOrOptions.digest, valueOrOptions.byteLength, [`p-${decisionId}`])
  }
  const { refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = valueOrOptions
  return Strength.eventPrepared('owner', decision, 'run-1', 'replica', 'K1', 'anchor-a', digest, 123, refs)
}
const promoted = (valueOrOptions = {}, decisionId = 'd1', target = 'run-1') => {
  if (valueOrOptions && typeof valueOrOptions === 'object' && 'digest' in valueOrOptions && !('refs' in valueOrOptions)) {
    return Strength.eventPromoted('owner', decisionId, target, valueOrOptions.digest, [`p-${decisionId}`])
  }
  const { refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = valueOrOptions
  return Strength.eventPromoted('owner', decision, 'run-1', digest, refs)
}
const apply = (state, event) => {
  const result = Strength.projectionApply(state, event)
  assert.equal(result.ok, true, result.error)
  return result.value
}
const turn = (providerRun, parts, outcome = 'completed') => ({ sessionId: 'owner', physicalUserMessageId: 'user-1', authorityRootUserMessageId: 'user-1', providerRun, parts, outcome })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const append = async (store, event) => Strength.storeAppend(store, H, event)
const writePayload = async (store, text) => {
  const result = await Strength.storeWritePayload(store, new TextEncoder().encode(text))
  assert.equal(result.ok, true)
  return result.value
}
const text = (value) => ({ kind: 'text', text: value })
const reasoning = (value) => ({ kind: 'reasoning', text: value })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const activity = (kind) => ({ kind, text: '' })

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact', () => {
  assert.equal(Strength.commitResolvePromotion('Committed', 'Unknown'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('Rejected', 'Unknown'), 'FailClosed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Matches'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Absent'), 'RetryAppend')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Unknown'), 'FailClosed')
})

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output', () => {
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'RealOutput'), 'Promote')
  assert.equal(Strength.promotionDecide('run-1', 'run-2', 'RealOutput'), 'IgnoreWrongRun')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'NoOutput'), 'AwaitOrAbandon')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'TransportOnly'), 'AwaitOrAbandon')
})

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

test('WHAT[SPEC-INV-007] STRENGTH_007_promotion_without_prepared_is_missing_parent', async () => {
  const local = createLocalEventStore()
  try {
    const frameRef = await writePayload(local.store, 'frame')
    const rejected = await append(local.store, promoted({ refs: [frameRef] }))
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'MissingParent')
  } finally { local.close() }
})

test('WHAT[SPEC-INV-007] STRENGTH_007_integrator_Current_reflects_Promoted_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, prepared({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, promoted({ refs: [ref] }))).ok, true)
    assert.equal(Strength.projectionIsPromoted('d1', Strength.storeCurrent(local.store)), true)
  } finally { local.close() }
})

test('WHAT[SPEC-INV-007] STRENGTH_007_provider_output_evidence_rejects_unknown_part_kinds', () => {
  const result = Strength.turnEvidenceClassify([{ kind: 'unknown-part' }])
  assert.equal(result.ok, false)
  assert.match(result.error, /unknown message part kind/)
})

test('WHAT[SPEC-INV-007] STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping', () => {
  assert.equal(Strength.turnEvidenceClassify([]), 'NoOutput')
  assert.equal(Strength.turnEvidenceClassify([activity('step-start')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([result('c1', 'result')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([text('answer')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([reasoning('thought')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([call('c1', 'read', '{}')]), 'RealOutput')
})
