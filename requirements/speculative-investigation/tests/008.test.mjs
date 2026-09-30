import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => createHash('sha256').update(text).digest('hex')
const frame = () => Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [
  { toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' },
  { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' },
] }]).value
const request = (decisionId = 'd1', target = 'run-1') => Strength.eventRequested({
  decisionId, ownerSessionId: 'owner',
  ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: target,
  sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
})
const bound = (decisionId = 'd1', target = 'run-1') => Strength.eventBound(decisionId, target, `replica-${decisionId}`, 'anchor-a')
const prepared = (value, decisionId = 'd1', target = 'run-1') => Strength.eventPrepared('owner', decisionId, target, `replica-${decisionId}`, 'anchor-a', value.digest, value.byteLength, [`p-${decisionId}`])
const promoted = (value, decisionId = 'd1', target = 'run-1') => Strength.eventPromoted('owner', decisionId, target, value.digest, [`p-${decisionId}`])
const apply = (state, event) => {
  const result = Strength.projectionApply(state, event)
  assert.equal(result.ok, true, result.error)
  return result.value
}

test('WHAT[speculative-investigation-008] STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor', async () => {
  const value = frame()
  let projection = apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared(value))
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
test('WHAT[speculative-investigation-008] STRENGTH_008_replay_loads_each_selected_plan_once_in_decision_order_and_stops_on_load_failure', async () => {
  const value = frame()
  let projection = Strength.projectionEmpty()

  for (const decisionId of ['d2', 'd1', 'd3']) {
    projection = apply(projection, request(decisionId, `run-${decisionId}`))
    projection = apply(projection, bound(decisionId, `run-${decisionId}`))
    projection = apply(projection, prepared(value, decisionId, `run-${decisionId}`))
    projection = apply(projection, promoted(value, decisionId, `run-${decisionId}`))
  }

  projection = apply(projection, request('d0', 'run-d0'))

  const messages = [{ id: 'user-1' }, ...['d3', 'd1', 'd2'].map((decisionId) => ({ id: `run-${decisionId}` }))]
  const successfulLoads = ['d3', 'd2', 'd1'].map((decisionId) => ({ decisionId, bundle: value }))
  const replay = await Strength.lifecycleReplayPlansObserved('owner', messages, successfulLoads, projection)

  assert.equal(replay.ok, true, replay.error)
  assert.deepEqual(replay.loadedDecisionIds, ['d1', 'd2', 'd3'])
  assert.deepEqual(replay.value.map((plan) => plan.prepared.decisionId), ['d1', 'd2', 'd3'])
  assert.deepEqual(replay.value.map((plan) => plan.beforeMessageIndex), [2, 3, 1])

  const failed = await Strength.lifecycleReplayPlansObserved('owner', messages, [
    { decisionId: 'd1', bundle: value },
    { decisionId: 'd2', error: 'load d2 failed' },
    { decisionId: 'd3', bundle: value },
  ], projection)
  assert.equal(failed.ok, false)
  assert.equal(failed.error, 'load d2 failed')
  assert.deepEqual(failed.loadedDecisionIds, ['d1', 'd2'])

  const unavailable = await Strength.lifecycleReplayPlansObserved('owner', messages, [
    { decisionId: 'd1', bundle: value },
  ], projection)
  assert.equal(unavailable.ok, false)
  assert.match(unavailable.error, /load unavailable.*d2/i)
  assert.deepEqual(unavailable.loadedDecisionIds, ['d1', 'd2'])

  const messagesWithoutFirstAnchor = messages.map((message) => message.id === 'run-d1' ? {} : message)
  const missingAnchor = await Strength.lifecycleReplayPlansObserved('owner', messagesWithoutFirstAnchor, successfulLoads, projection)
  assert.equal(missingAnchor.ok, false)
  assert.match(missingAnchor.error, /target anchor is absent.*run-d1/i)
  assert.deepEqual(missingAnchor.loadedDecisionIds, [])

  const noneSelected = await Strength.lifecycleReplayPlansObserved('owner', messages, successfulLoads, Strength.projectionEmpty())
  assert.equal(noneSelected.ok, true)
  assert.deepEqual(noneSelected.value, [])
  assert.deepEqual(noneSelected.loadedDecisionIds, [])
})
test('WHAT[speculative-investigation-008] STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage', async () => {
  const value = frame()
  let projection = apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared(value))
  projection = apply(projection, promoted(value))
  projection = apply(projection, Strength.eventTraced('d1', 40n, 44n))
  const plan = (await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)).value[0]
  assert.equal(Strength.lifecycleNeedsRawReplay(null, plan), true)
  assert.equal(Strength.lifecycleNeedsRawReplay(42n, plan), true)
})
test('WHAT[speculative-investigation-008] STRENGTH_008_shortened_conversation_never_erases_the_authorization', async () => {
  const value = frame()
  let projection = apply(apply(apply(Strength.projectionEmpty(), request()), bound()), prepared(value))
  projection = apply(projection, promoted(value))
  projection = apply(projection, Strength.eventTraced('d1', 10n, 14n))
  // A compaction-sized transcript still keeps the durable authorization and the
  // promoted material that has not yet been covered by XTrace.
  assert.equal(Strength.projectionRequestedRounds('d1', projection), 2)
  const whole = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)
  assert.equal(whole.ok, true)
  assert.deepStrictEqual(whole.value[0].existingTraceRange, { startInclusive: 10n, endExclusive: 14n })
  // Dropping the anchor from the transcript fails closed instead of silently
  // forgetting the frame; the authorization itself is untouched.
  const short = await Strength.lifecycleReplayPlans('owner', [{ id: 'run-1' }], value, projection)
  assert.equal(short.ok, false)
  assert.match(short.error, /target anchor is absent/i)
  assert.equal(Strength.projectionRequestedRounds('d1', projection), 2)
})
test('WHAT[speculative-investigation-008] STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const H = (text) => createHash('sha256').update(text).digest('hex')
const request = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventRequested({
  decisionId: decision, ownerSessionId: 'owner',
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

test('WHAT[speculative-investigation-008] STRENGTH_008_integrator_Current_reflects_Traced_range_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, request())).ok, true)
    assert.equal((await append(local.store, Strength.eventBound('d1', 'run-1', 'replica', 'anchor-a'))).ok, true)
    assert.equal((await append(local.store, prepared({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, promoted({ refs: [ref] }))).ok, true)
    assert.equal((await append(local.store, Strength.eventTraced('d1', 10n, 12n))).ok, true)
    const projection = Strength.storeCurrent(local.store)
    assert.equal(Strength.projectionIsPromoted('d1', projection), true)
    assert.deepEqual(Strength.projectionTraceRange('d1', projection), { startInclusive: 10n, endExclusive: 12n })
  } finally { local.close() }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[speculative-investigation-008] StrengthReplay owns applyBeforeXTrace entry point for replay before xtrace', () => {
  const replay = read('src/Wanxiangshu/Strength/OpenCode/Replay.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(replay, /let\s+applyBeforeXTrace/)
  assert.match(replay, /plansOrFailClosed/)
  assert.match(replay, /XTraceProjection\.tryContiguousHostRange/)
  assert.match(replay, /XTraceProjection\.orderedSemanticParts/)
  assert.doesNotMatch(replay, /XTraceProjection\.(?:tryHostMessageId|parts|currentGenerationParts)|XTracePartRef/)
  assert.doesNotMatch(replay, /stableHostIdOfProvenance|IndexOf\("\\\/part:|isContiguousFromFirst/)
  assert.match(pt, /StrengthReplay\.applyBeforeXTrace/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import('../../../dist/Strength/Surface.js');
const Projection = await import('../../../dist/Participant/Provider/Projection/Surface.js');
const Adapter = await import('../../../dist/OpenCode/Codec/ProviderProjectionSurface.js');
const { createLocalEventStore } = await import('../../verification-system/tests/support/local-event-store.mjs');

const H = (text) => createHash('sha256').update(text).digest('hex')

const frame = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [
  { toolName: 'read', canonicalArguments: '{"filePath":"src/a.fs"}', canonicalResult: 'let a = 1' },
  { toolName: 'grep', canonicalArguments: '{"pattern":"a"}', canonicalResult: 'src/a.fs:1:let a = 1' },
] }]).value
// A persisted frame payload is the Store wire contract (encodeFrameBundlePayload):
// version + digest + byte_length + batches[request_ordinal/exchanges[tool_name/arguments/result]].
// The JS frame shape is the Surface shape, not the payload shape; serializing it
// verbatim makes decodeFrameBundlePayload refuse the load.
const storeWirePayload = (bundle) => ({
  version: 1,
  digest: bundle.digest,
  byte_length: bundle.byteLength,
  batches: bundle.batches.map((b) => ({
    request_ordinal: b.requestOrdinal,
    exchanges: b.exchanges.map((e) => ({ tool_name: e.toolName, arguments: e.canonicalArguments, result: e.canonicalResult })),
  })),
})

const text = (value) => ({ kind: 'text', text: value })
const message = (role, parts) => ({ role, parts })
const snapshot = (messages) => Projection.projectionSnapshot(Projection.semanticProjection(messages))

const append = async (durability, event) => {
  const result = await Strength.durabilityAppend(durability, event)
  assert.equal(result.ok, true, result.error)
}

integrationTest('WHAT[speculative-investigation-008] STRENGTH_INTEGRATION_Authorization_Bound_Prepared_consumption_Promoted_restart_replay_Traced', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const decision = 'decision-1'
    await append(durability, Strength.eventRequested({
      decisionId: decision, ownerSessionId: 'owner',
      ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
      sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
      sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
    }))
    await append(durability, Strength.eventBound(decision, 'run-1', 'replica-1', 'anchor-1'))
    const ref = await Strength.storeWritePayload(local.store, new TextEncoder().encode(JSON.stringify(storeWirePayload(frame))))
    assert.equal(ref.ok, true)
    await append(durability, Strength.eventPrepared('owner', decision, 'run-1', 'replica-1', 'anchor-1', frame.digest, frame.byteLength, [ref.value]))

    let loaded = await Strength.durabilityLoadProjection(durability)
    assert.equal(loaded.ok, true)
    let projection = loaded.value
    assert.equal(Strength.projectionRequestedRounds(decision, projection), 2)
    assert.equal(Strength.projectionIsPromoted(decision, projection), false)
    assert.equal((await Strength.lifecycleReplayPlans('owner', [{ id: 'run-1' }], frame, projection)).value.length, 0)

    // The payload written above must be the real material: the production
    // recovery entry decodes it through the same path a restart would use and
    // returns the same batches, byteLength and digest as the frame it was built
    // from. A storeWirePayload transcription drift — a renamed or mis-nested key —
    // makes this load refuse instead of passing.
    const reloaded = await Strength.durabilityLoadBundleForDecision(durability, projection, decision)
    assert.equal(reloaded.ok, true, reloaded.error)
    assert.equal(reloaded.value.digest, frame.digest)
    assert.equal(reloaded.value.byteLength, frame.byteLength)
    assert.equal(reloaded.value.batches.length, frame.batches.length)
    for (const [index, batch] of frame.batches.entries()) {
      const loadedBatch = reloaded.value.batches[index]
      assert.equal(loadedBatch.requestOrdinal, batch.requestOrdinal)
      assert.equal(loadedBatch.exchanges.length, batch.exchanges.length)
      for (const [position, exchange] of batch.exchanges.entries()) {
        const loadedExchange = loadedBatch.exchanges[position]
        assert.equal(loadedExchange.toolName, exchange.toolName)
        assert.equal(loadedExchange.canonicalArguments, exchange.canonicalArguments)
        assert.equal(loadedExchange.canonicalResult, exchange.canonicalResult)
      }
    }

    const current = [message('user', [text('inspect the file')])]
    const candidateIntent = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: decision, targetProviderRun: 'run-1', currentProviderRun: 'run-1', bundle: frame }).value
    const candidateWire = Projection.renderMessagesWithHostIds(snapshot(current), current, [candidateIntent])
    assert.deepEqual(candidateWire.messages.map((item) => item.role), ['user', 'assistant', 'tool'])

    const consumed = {
      sessionId: 'owner', physicalUserMessageId: 'user-1', authorityRootUserMessageId: 'user-1', providerRun: 'run-1',
      parts: [{ kind: 'tool-call', callId: 'real-call', name: 'read', args: '{}' }], outcome: 'completed',
    }
    const promotion = Strength.lifecycleReconcileHandle(projection, consumed)
    assert.equal(promotion.view.kind, 'Promoted')
    await append(durability, promotion.event)

    const restarted = Strength.durabilityCreate(local.store)
    projection = (await Strength.durabilityLoadProjection(restarted)).value
    assert.equal(Strength.projectionIsPromoted(decision, projection), true)

    const baseWire = [message('user', [text('inspect the file')]), message('assistant', [text('primary output')]), message('user', [text('continue')])]
    const rawResult = Adapter.tryApplyRenderedMessages('owner', H, { messages: baseWire, hostMessageIds: ['user-1', 'run-1', 'user-2'], hostIsPhysical: [false, false, false] })
    assert.equal(rawResult.ok, true)
    const rawBase = rawResult.value
    const replayPlans = await Strength.lifecycleReplayPlans('owner', rawBase.map((value, index) => ({ id: ['user-1', 'run-1', 'user-2'][index] })), frame, projection)
    assert.equal(replayPlans.ok, true)
    const [plan] = replayPlans.value
    assert.equal(plan.beforeMessageIndex, 1)

    const replayIntents = Strength.lifecycleReplayIntents(H, replayPlans.value, '')
    assert.equal(replayIntents.ok, true)
    const replayed = Projection.renderMessagesWithHostIds(snapshot(baseWire), baseWire, replayIntents.value)
    const written = Adapter.tryApplyRenderedInsertionsPreservingBase('owner', H, rawBase, replayed)
    assert.equal(written.ok, true)
    assert.deepEqual(written.value[0], rawBase[0], 'the preserved base rows are structurally identical, not the same reference')
    assert.deepEqual(written.value[3], rawBase[1])
    assert.deepEqual(Adapter.decodeMessageView(written.value).messages.map((item) => item.role), ['user', 'assistant', 'tool', 'assistant', 'user'])

    await append(restarted, Strength.eventTraced(decision, 20n, 24n))
    projection = (await Strength.durabilityLoadProjection(restarted)).value
    const tracedPlans = (await Strength.lifecycleReplayPlans('owner', rawBase.map((value, index) => ({ id: ['user-1', 'run-1', 'user-2'][index] })), frame, projection)).value
    const [traced] = tracedPlans
    assert.equal(Strength.lifecycleNeedsRawReplay(22n, traced), true)
    assert.equal(Strength.lifecycleNeedsRawReplay(23n, traced), false)
  } finally { local.close() }
})

test('WHAT[speculative-investigation-008] H03_owner_replayed_replica_frame_with_positive_estimates_never_reaggregates_DelegationRequested', async () => {
  // Mutation 验证与退化路径说明：
  // 若 Delegate.fs 中的 resolveCompletedSourceBatch 发生退化，错误地将历史回放中已 Promoted 的 Replica 帧
  // 当作 owner 当前新鲜完成的来源批次，则 tryCapture 会错误判定为正数估计并追加新的 DelegationRequested。
  // 本测试证明：owner 历史上回放带有 positive estimated_readonly_rounds 的 Replica 帧后，
  // transform / tryCapture 决不产生新的 DelegationRequested，且 R02（原参数保真）与 R14（镜像重定位 arguments 不变）成立。
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const decision = 'decision-h03'
    const ownerSessionId = 'owner-h03'

    // 1. 构造包含正数 estimated_readonly_rounds 与合法 self_note 的 Replica 帧
    const replicaExchanges = [
      {
        toolName: 'read',
        canonicalArguments: JSON.stringify({
          filePath: 'src/Wanxiangshu/Strength/Runtime.fs',
          estimated_readonly_rounds: 3,
          self_note: 'inspect isExactReadonly invariant'
        }),
        canonicalResult: 'let isExactReadonly capabilities = ...'
      },
      {
        toolName: 'grep',
        canonicalArguments: JSON.stringify({
          pattern: 'exactReadonlyHostToolMap',
          estimated_readonly_rounds: 2,
          self_note: 'verify host tool map entries'
        }),
        canonicalResult: 'src/Wanxiangshu/Strength/Runtime.fs:51:exactReadonlyHostToolMap'
      }
    ]

    const replicaBundle = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: replicaExchanges }]).value
    assert.ok(replicaBundle, 'Replica bundle must build successfully')

    // R02: 断言原数值与短记在 frame bundle 中准确恢复
    const firstExchangeArgs = JSON.parse(replicaBundle.batches[0].exchanges[0].canonicalArguments)
    assert.equal(firstExchangeArgs.estimated_readonly_rounds, 3)
    assert.equal(firstExchangeArgs.self_note, 'inspect isExactReadonly invariant')
    const secondExchangeArgs = JSON.parse(replicaBundle.batches[0].exchanges[1].canonicalArguments)
    assert.equal(secondExchangeArgs.estimated_readonly_rounds, 2)
    assert.equal(secondExchangeArgs.self_note, 'verify host tool map entries')

    // 2. 写入授权生命周期事件：Requested -> Bound -> Prepared -> Promoted
    await append(durability, Strength.eventRequested({
      decisionId: decision, ownerSessionId: ownerSessionId,
      ownerLogicalRun: { logicalRunId: 'logical-h03', authorityRootUserMessageId: 'user-h03' },
      sourcePhysicalUserMessageId: 'user-h03', sourceProviderRun: 'run-h03',
      sourceToolCallIds: ['call-h03'], requestedRounds: 3, contractRevision: 2,
    }))
    await append(durability, Strength.eventBound(decision, 'run-h03', 'replica-h03', 'anchor-h03'))

    const payloadRef = await Strength.storeWritePayload(local.store, new TextEncoder().encode(JSON.stringify(storeWirePayload(replicaBundle))))
    assert.equal(payloadRef.ok, true)
    await append(durability, Strength.eventPrepared(ownerSessionId, decision, 'run-h03', 'replica-h03', 'anchor-h03', replicaBundle.digest, replicaBundle.byteLength, [payloadRef.value]))
    await append(durability, Strength.eventPromoted(ownerSessionId, decision, 'run-h03', replicaBundle.digest, [payloadRef.value]))

    let projection = (await Strength.durabilityLoadProjection(durability)).value
    assert.equal(Strength.projectionIsPromoted(decision, projection), true)

    // 3. 在 owner 历史上回放该 Replica 帧
    const baseWire = [
      message('user', [text('investigate runtime permissions')]),
      message('assistant', [text('here is the primary plan')]),
      message('user', [text('continue with verification')])
    ]
    const rawResult = Adapter.tryApplyRenderedMessages(ownerSessionId, H, {
      messages: baseWire,
      hostMessageIds: ['user-h03', 'run-h03', 'user-h03-cont'],
      hostIsPhysical: [true, false, true]
    })
    assert.equal(rawResult.ok, true)
    const rawBase = rawResult.value

    const replayPlans = await Strength.lifecycleReplayPlans(ownerSessionId, rawBase.map((v, i) => ({ id: ['user-h03', 'run-h03', 'user-h03-cont'][i] })), replicaBundle, projection)
    assert.equal(replayPlans.ok, true)
    assert.equal(replayPlans.value.length, 1)

    const replayIntents = Strength.lifecycleReplayIntents(H, replayPlans.value, '')
    assert.equal(replayIntents.ok, true)
    const replayed = Projection.renderMessagesWithHostIds(snapshot(baseWire), baseWire, replayIntents.value)
    const written = Adapter.tryApplyRenderedInsertionsPreservingBase(ownerSessionId, H, rawBase, replayed)
    assert.equal(written.ok, true)

    // R14: 镜像 ID 重定位后，tool call 的 arguments 保持不变
    const decodedTranscript = Adapter.decodeMessageView(written.value)
    const toolCallPart = decodedTranscript.messages.flatMap((m) => m.parts).find((p) => p.kind === 'ToolCall' || p.kind === 'tool-call')
    assert.ok(toolCallPart, 'Replayed transcript must contain replayed tool-call part')
    const replayedCallArgs = JSON.parse(toolCallPart.args)
    assert.equal(replayedCallArgs.estimated_readonly_rounds, 3)
    assert.equal(replayedCallArgs.self_note, 'inspect isExactReadonly invariant')

    // 4. H03: 在回传历史之后执行 owner 的 transform / tryCapture，断言绝不重新聚合生成新的 DelegationRequested
    const rawDelegate = await import('../../../dist/Strength/OpenCode/Delegate.js')
    const rawPluginScope = await import('../../../dist/Strength/OpenCode/PluginScope.js')

    const strengthScope = new rawPluginScope.PluginStrengthScope()
    strengthScope.AttachStrengthReplicaRuntime({})

    const snapshotPort = {
      GetMessages: async () => ({
        tag: 0,
        fields: [[{ Id: 'run-h03', Role: 'assistant', ParentId: 'user-h03' }]],
      }),
    }
    const journal = {
      Snapshot: () => ({
        AgentProjections: {
          Associations: new Map([[ownerSessionId, [{ tag: 0 }, { tag: 0 }]]]),
          Profiles: new Map([
            [
              ownerSessionId,
              {
                CanonicalRole: 'engineer',
                AuthorityKind: { tag: 0 },
                LogicalRunId: 'logical-h03',
                AuthorityRootUserMessageId: 'user-h03',
              },
            ],
          ]),
        },
      }),
    }

    const appendedCaptureEvents = []
    const captureDurability = {
      LoadProjection: async () => ({ tag: 0, fields: [projection] }),
      Append: async (event) => {
        appendedCaptureEvents.push(event)
        return { tag: 0 }
      },
    }

    const transformOutput = {
      messages: written.value
    }

    const captureOutcome = await rawDelegate.tryCapture(
      snapshotPort,
      journal,
      captureDurability,
      strengthScope,
      () => null,
      null,
      true,
      transformOutput
    )

    // 断言：回传历史绝不被识别为新的委托请求来源
    assert.notEqual(rawDelegate.captureOutcomeCode(captureOutcome), 'Captured', 'Replayed Replica history must not produce a Captured outcome')
    assert.equal(appendedCaptureEvents.length, 0, 'No DelegationRequested event may be appended from replayed history')
  } finally {
    local.close()
  }
})

}
