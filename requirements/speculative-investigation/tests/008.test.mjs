import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

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

test('WHAT[speculative-investigation-008] STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor', async () => {
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
test('WHAT[speculative-investigation-008] STRENGTH_008_replay_loads_each_selected_plan_once_in_decision_order_and_stops_on_load_failure', async () => {
  const value = frame()
  let projection = Strength.projectionEmpty()

  for (const decisionId of ['d2', 'd1', 'd3']) {
    projection = apply(projection, prepared(value, decisionId, `run-${decisionId}`))
    projection = apply(projection, promoted(value, decisionId, `run-${decisionId}`))
  }

  projection = apply(projection, prepared(value, 'd0', 'run-d0'))

  const messages = ['d3', 'd1', 'd2'].map((decisionId) => ({ id: `run-${decisionId}` }))
  const successfulLoads = ['d3', 'd2', 'd1'].map((decisionId) => ({ decisionId, bundle: value }))
  const replay = await Strength.lifecycleReplayPlansObserved('owner', messages, successfulLoads, projection)

  assert.equal(replay.ok, true, replay.error)
  assert.deepEqual(replay.loadedDecisionIds, ['d1', 'd2', 'd3'])
  assert.deepEqual(replay.value.map((plan) => plan.prepared.decisionId), ['d1', 'd2', 'd3'])
  assert.deepEqual(replay.value.map((plan) => plan.beforeMessageIndex), [1, 2, 0])

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
  let projection = apply(Strength.projectionEmpty(), prepared(value))
  projection = apply(projection, promoted(value))
  projection = apply(projection, Strength.eventTraced('d1', 40n, 44n))
  const plan = (await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)).value[0]
  assert.equal(Strength.lifecycleNeedsRawReplay(null, plan), true)
  assert.equal(Strength.lifecycleNeedsRawReplay(42n, plan), true)
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
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Fission = await import("../../../dist/Execution/Fission/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.peer,
      canonicalRole: resolved.identity.role,
      selectedTier: resolved.identity.initialTier.toLowerCase(),
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const ownerProfile = (agent = 'engineer') => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-special-lineage',
    'ses_special_owner',
    'HumanRoot',
    'msg_special_owner',
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const binding = (owner, replica, decision, role = 'Coder', budget = 'K1') => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, role, budget, 65536, `sem-${decision}`, [])
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const replicaBinding = (owner, replica, decision, budget) => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Coder', budget, 65536, `sem-${decision}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const attach = (replica, budget, purpose = 'Treatment', owner = 'owner') => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const decision = `decision-${replica}`
  const result = Strength.replicaAttach(handle, replicaBinding(owner, replica, decision, budget), purpose)
  assert.equal(result.ok, true, result.error)
  return { handle, completion: result.value.completion }
}
const turn = (sessionId, outcome, providerRun = 'run-t') => ({ sessionId, providerRun, outcome, parts: [] })
const oneBatch = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] })

}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const H = (text) => `H(${text})`
const prepared = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPrepared('owner', decision, 'run-1', 'replica', 'K1', 'anchor-a', digest, 123, refs)
const promoted = ({ refs = ['payload-a'], digest = 'frame-a', decision = 'd1' } = {}) => Strength.eventPromoted('owner', decision, 'run-1', digest, refs)
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
