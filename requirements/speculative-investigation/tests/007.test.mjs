import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

test('WHAT[speculative-investigation-007] STRENGTH_007_promotion_commit_unknown_never_allows_continuation_without_durable_fact', () => {
  assert.equal(Strength.commitResolvePromotion('Committed', 'Unknown'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('Rejected', 'Unknown'), 'FailClosed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Matches'), 'Proceed')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Absent'), 'RetryAppend')
  assert.equal(Strength.commitResolvePromotion('CommitUnknown', 'Unknown'), 'FailClosed')
})
test('WHAT[speculative-investigation-007] STRENGTH_007_promotion_requires_the_exact_target_run_and_real_provider_output', () => {
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'RealOutput'), 'Promote')
  assert.equal(Strength.promotionDecide('run-1', 'run-2', 'RealOutput'), 'IgnoreWrongRun')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'NoOutput'), 'AwaitOrAbandon')
  assert.equal(Strength.promotionDecide('run-1', 'run-1', 'TransportOnly'), 'AwaitOrAbandon')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => createHash('sha256').update(text).digest('hex')
const bundle = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [
  { toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' },
  { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' },
] }]).value
const request = (decisionId = 'd1') => Strength.eventRequested({
  decisionId, ownerSessionId: 'owner',
  ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
})
const bound = (decisionId = 'd1') => Strength.eventBound(decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a')
const prepared = (decisionId = 'd1') => Strength.eventPrepared('owner', decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a', bundle.digest, bundle.byteLength, [`p-${decisionId}`])
const promoted = (decisionId = 'd1') => Strength.eventPromoted('owner', decisionId, 'run-1', bundle.digest, [`p-${decisionId}`])
const apply = (state, event) => {
  const result = Strength.projectionApply(state, event)
  assert.equal(result.ok, true, result.error)
  return result.value
}
const turn = (providerRun, parts, outcome = 'completed') => ({ sessionId: 'owner', physicalUserMessageId: 'user-1', authorityRootUserMessageId: 'user-1', providerRun, parts, outcome })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })

test('WHAT[speculative-investigation-007] STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output', () => {
  let projection = apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared())
  const realTurn = turn('run-1', [call('c1', 'read', '{}')])
  const eventView = Strength.lifecycleReconcileEvent(projection, realTurn)
  assert.equal(eventView.kind, 'Promoted')
  assert.equal(eventView.decisionId, 'd1')
  assert.equal(eventView.frameDigest, bundle.digest)
  // A different run never consumes the authorization.
  assert.equal(Strength.lifecycleReconcileEvent(projection, { ...realTurn, providerRun: 'other' }), null)
  // An empty failed run is abandoned, not promoted.
  const abandonedView = Strength.lifecycleReconcileEvent(projection, turn('run-1', [call('partial', 'read', '{}')], 'failed'))
  assert.equal(abandonedView.kind, 'Abandoned')
  projection = apply(projection, Strength.eventAbandoned('d1', 'run-1'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), null)
  // A promoted authorization is consumed once: no second promotion.
  projection = apply(apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared()), promoted())
  assert.equal(Strength.lifecycleReconcileEvent(projection, realTurn), null)
})
test('WHAT[speculative-investigation-007] STRENGTH_007_promotion_must_repeat_the_exact_prepared_material', () => {
  const ready = apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared())
  const result = Strength.projectionApply(ready, Strength.eventPromoted('owner', 'd1', 'run-1', 'other-digest', ['p-d1']))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'PromotionMismatch')
})
test('WHAT[speculative-investigation-007] STRENGTH_007_closed_authorization_is_never_restarted_or_promoted', () => {
  let projection = apply(apply(Strength.projectionEmpty(), request()), bound())
  projection = apply(projection, Strength.eventClosed('d1', 'Bound', 'CannotContinue'))
  const closed = Strength.projectionCandidate('d1', projection)
  assert.equal(closed.state, 'Closed')
  assert.equal(closed.request.requestedRounds, 2, 'the authorization stays readable after closing')
  const promoted = Strength.projectionApply(projection, Strength.eventPromoted('owner', 'd1', 'run-1', bundle.digest, ['p-d1']))
  assert.equal(promoted.ok, false)
  assert.equal(promoted.error, 'PromotionAfterAbandon')
  const reopened = Strength.projectionApply(projection, request())
  assert.equal(Strength.projectionCandidate('d1', reopened.ok ? reopened.value : projection).state, 'Closed')
})
test('WHAT[speculative-investigation-007] STRENGTH_007_closed_authorization_releases_the_target_for_a_new_request', () => {
  let projection = apply(apply(Strength.projectionEmpty(), request('d1')), bound('d1'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), 'd1')
  projection = apply(projection, Strength.eventClosed('d1', 'Bound', 'CannotContinue'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), null)
  assert.equal(Strength.projectionRequestedRounds('d1', projection), 2)
  const request2 = Strength.eventRequested({
    decisionId: 'd2', ownerSessionId: 'owner',
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-2', sourceProviderRun: 'run-2',
    sourceToolCallIds: ['call-2'], requestedRounds: 3, contractRevision: 1,
  })
  projection = apply(apply(projection, request2), bound('d2'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), 'd2')
})
test('WHAT[speculative-investigation-007] STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping', () => {
  const text = (value) => ({ kind: 'text', text: value })
  const reasoning = (value) => ({ kind: 'reasoning', text: value })
  const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
  const activity = (kind) => ({ kind, text: '' })
  const unknown = Strength.turnEvidenceClassify([{ kind: 'unknown-part' }])
  assert.equal(unknown.ok, false)
  assert.match(unknown.error, /unknown message part kind/)
  assert.equal(Strength.turnEvidenceClassify([]), 'NoOutput')
  assert.equal(Strength.turnEvidenceClassify([activity('step-start')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([result('c1', 'result')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([text('answer')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([reasoning('thought')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([call('c1', 'read', '{}')]), 'RealOutput')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const H = (text) => createHash('sha256').update(text).digest('hex')
const request = () => Strength.eventRequested({
  decisionId: 'd1', ownerSessionId: 'owner',
  ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
})
const promoted = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPromoted('owner', decision, 'run-1', digest, refs)
const prepared = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPrepared('owner', decision, 'run-1', 'replica', 'anchor-a', digest, 123, refs)
const append = async (store, event) => Strength.storeAppend(store, H, event)
const writePayload = async (store, text) => {
  const result = await Strength.storeWritePayload(store, new TextEncoder().encode(text))
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[speculative-investigation-007] STRENGTH_007_promotion_without_prepared_is_missing_parent', async () => {
  const local = createLocalEventStore()
  try {
    const frameRef = await writePayload(local.store, 'frame')
    assert.equal((await append(local.store, request())).ok, true)
    assert.equal((await append(local.store, Strength.eventBound('d1', 'run-1', 'replica', 'anchor-a'))).ok, true)
    const rejected = await append(local.store, promoted({ refs: [frameRef] }))
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'MissingParent')
  } finally { local.close() }
})
test('WHAT[speculative-investigation-007] STRENGTH_007_integrator_Current_reflects_Promoted_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, request())).ok, true)
    assert.equal((await append(local.store, Strength.eventBound('d1', 'run-1', 'replica', 'anchor-a'))).ok, true)
    assert.equal((await append(local.store, prepared({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, promoted({ refs: [ref] }))).ok, true)
    assert.equal(Strength.projectionIsPromoted('d1', Strength.storeCurrent(local.store)), true)
  } finally { local.close() }
})
}
