import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");


test('WHAT[speculative-investigation-006] STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing', () => {
  assert.equal(Strength.commitResolvePrepared('Committed', 'Unknown'), 'Proceed')
  assert.equal(Strength.commitResolvePrepared('Rejected', 'Unknown'), 'FallBackK0')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Matches'), 'Proceed')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Absent'), 'FallBackK0')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Unknown'), 'FailClosed')
  assert.equal(Strength.commitResolvePrepared('CommitUnknown', 'Conflicts'), 'FailClosed')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const H = (text) => createHash('sha256').update(text).digest('hex')
const frame = (toolName = 'read', args = '{"filePath":"a"}', result = 'alpha') => Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName, canonicalArguments: args, canonicalResult: result }] }]).value
const publishRequest = (bundle, replica = 'replica-1') => ({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'run-1', replicaSessionId: replica, budget: 'K1', anchorDigest: 'anchor-a', bundle })

test('WHAT[speculative-investigation-006] STRENGTH_006_008_durability_port_publishes_payload_closure_and_reloads_the_same_bundle', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(bundle))).kind, 'Published')
    let projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('d1', projection)
    assert.equal(view.prepared.frameDigest, bundle.digest)
    assert.equal(view.prepared.materialPayloads.length, 1)
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'd1')
    assert.equal(loaded.ok, true)
    assert.equal(loaded.value.digest, bundle.digest)
    assert.equal(loaded.value.byteLength, bundle.byteLength)
    assert.equal((await Strength.durabilityAppend(durability, Strength.eventPromoted('owner', 'd1', 'run-1', bundle.digest, view.prepared.materialPayloads))).ok, true)
    assert.equal((await Strength.durabilityAppend(durability, Strength.eventTraced('d1', 5n, 7n))).ok, true)
    projection = (await Strength.durabilityLoadProjection(durability)).value
    assert.equal(Strength.projectionIsPromoted('d1', projection), true)
    assert.deepEqual(Strength.projectionTraceRange('d1', projection), { startInclusive: 5n, endExclusive: 7n })
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] STRENGTH_006_durability_port_rejects_conflicting_Prepared_identity', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const first = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(first))).kind, 'Published')
    const other = frame('grep', '{"pattern":"x"}', 'a:1:x')
    const conflict = await Strength.durabilityPublishPrepared(durability, publishRequest(other, 'replica-2'))
    assert.equal(conflict.kind, 'StorageInvalid')
  } finally { local.close() }
})
}

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

test('WHAT[speculative-investigation-006] STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed', async () => {
  const value = frame()
  const projection = apply(Strength.projectionEmpty(), prepared(value))
  assert.equal(Strength.projectionIsPromoted('d1', projection), false)
  const traced = Strength.projectionApply(projection, Strength.eventTraced('d1', 10n, 14n))
  assert.equal(traced.ok, false)
  const replay = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], value, projection)
  assert.equal(replay.ok, true)
  assert.equal(replay.value.length, 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const makeDir = (prefix) => mkdtempSync(join(tmpdir(), prefix))
const H = (text) => createHash('sha256').update(text).digest('hex')
const frame = (toolName = 'read', args = '{"filePath":"a"}', result = 'alpha') =>
  Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName, canonicalArguments: args, canonicalResult: result }] }]).value
const publishRequest = (bundle, decision = 'cut-d1', replica = 'replica-1') => ({
  ownerSessionId: 'owner',
  decisionId: decision,
  targetProviderRun: 'run-1',
  replicaSessionId: replica,
  budget: 'K1',
  anchorDigest: 'anchor-a',
  bundle,
})

test('WHAT[speculative-investigation-006] prepared_cut_legal_command_is_accepted_by_the_fold', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    const published = await Strength.durabilityPublishPrepared(durability, publishRequest(bundle))
    assert.equal(published.kind, 'Published')

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d1', projection)
    assert.equal(view.prepared.frameDigest, bundle.digest)
    assert.equal(view.prepared.byteLength, bundle.byteLength)
    assert.equal(view.prepared.materialPayloads.length, 1)

    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'cut-d1')
    assert.equal(loaded.ok, true)
    assert.equal(loaded.value.digest, bundle.digest)
    assert.equal(loaded.value.byteLength, bundle.byteLength)
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] prepared_cut_payload_closure_is_complete_before_the_receipt', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame('grep', '{"pattern":"x"}', 'a:1:x')
    const published = await Strength.durabilityPublishPrepared(durability, publishRequest(bundle, 'cut-d2'))
    assert.equal(published.kind, 'Published')

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d2', projection)
    assert.deepEqual(
      Object.keys(view.prepared).sort(),
      ['anchorDigest', 'budget', 'byteLength', 'decisionId', 'frameDigest', 'materialPayloads', 'ownerSessionId', 'replicaSessionId', 'targetProviderRun'].sort(),
      'the complete Prepared write set rides the durable fact, not one field',
    )
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] prepared_cut_reopen_observes_only_durable_receipts', async () => {
  const base = makeDir('wxs-strength-cut-reopen-')
  const commonDir = join(base, '.git')
  const local = createLocalEventStore({ commonDir })
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(bundle, 'cut-d3'))).kind, 'Published')
    const before = (await Strength.durabilityLoadProjection(durability)).value
    assert.ok(Strength.projectionCandidate('cut-d3', before))
  } finally { local.close() }

  const reopened = createLocalEventStore({ commonDir })
  try {
    const durability = Strength.durabilityCreate(reopened.store)
    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d3', projection)
    assert.ok(view, 'reopen replays the durable Prepared receipt into Current')
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'cut-d3')
    assert.equal(loaded.ok, true, 'payload closure survives reopen with the fact')
  } finally {
    reopened.close()
    rmSync(base, { recursive: true, force: true })
  }
})
test('WHAT[speculative-investigation-006] prepared_cut_has_no_optional_fatal_handler_path', async () => {
  const { readFileSync } = await import('node:fs')
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Strength/Persistence/Durability.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /fatalTripHandler|setFatalTripHandler/)
})
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

test('WHAT[speculative-investigation-006] STRENGTH_006_017_strength_event_types_are_authoritative_store_vocabulary', () => {
  assert.deepEqual([
    Strength.eventType(prepared()),
    Strength.eventType(promoted()),
    Strength.eventType(Strength.eventTraced('d1', 1n, 2n)),
    Strength.eventType(Strength.eventAbandoned('d1', 'run-1')),
  ], ['StrengthCandidatePrepared', 'StrengthCandidatePromoted', 'StrengthFramesTraced', 'StrengthCandidateAbandoned'])
})
test('WHAT[speculative-investigation-006] STRENGTH_006_store_envelope_puts_large_material_only_in_payload_refs', () => {
  const first = Strength.envelopeView(Strength.storeToEnvelope(H, prepared()))
  const conflicting = Strength.envelopeView(Strength.storeToEnvelope(H, prepared({ refs: ['payload-b'], digest: 'frame-b' })))
  assert.equal(first.eventType, 'StrengthCandidatePrepared')
  assert.deepEqual(first.payloadRefs, ['payload-a'])
  assert.equal(first.id, conflicting.id)
  const decoded = Strength.storeTryDecodeEnvelope(Strength.storeToEnvelope(H, prepared()))
  assert.equal(decoded.ok, true)
  assert.equal(decoded.value.kind, 'Prepared')
  assert.equal(decoded.value.frameDigest, 'frame-a')
})
test('WHAT[speculative-investigation-006] STRENGTH_006_same_decision_different_prepared_material_is_identity_collision', async () => {
  const local = createLocalEventStore()
  try {
    const firstRef = await writePayload(local.store, 'first')
    const secondRef = await writePayload(local.store, 'second')
    const first = Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-a', 5, [firstRef])
    const conflict = Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-b', 6, [secondRef])
    assert.equal((await append(local.store, first)).ok, true)
    const rejected = await append(local.store, conflict)
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'IdentityCollision')
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] STRENGTH_006_payload_bytes_are_local_content_addressed_payloads', async () => {
  const local = createLocalEventStore()
  try {
    const bytes = new Uint8Array([1, 2, 3, 4])
    const first = await Strength.storeWritePayload(local.store, bytes)
    const second = await Strength.storeWritePayload(local.store, bytes)
    assert.equal(first.value, second.value)
    const loaded = await Strength.storeReadPayload(local.store, first.value)
    assert.deepEqual([...loaded.value], [...bytes])
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] STRENGTH_006_integrator_Current_reflects_Prepared_binding_without_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    const ref = await writePayload(local.store, 'frame-material')
    assert.equal((await append(local.store, Strength.eventPrepared('owner', 'd1', 'run-1', 'replica', 'K1', 'anchor-a', 'frame-a', 14, [ref]))).ok, true)
    const projection = Strength.storeCurrent(local.store)
    assert.equal(Strength.projectionDecisionForTarget('run-1', projection), 'd1')
    assert.equal(Strength.projectionIsPromoted('d1', projection), false)
  } finally { local.close() }
})
test('WHAT[speculative-investigation-006] STRENGTH_006_prepared_event_persists_nominal_budget_without_duplicate_derived_value_estimates', () => {
  const envelope = Strength.storeToEnvelope(H, prepared({ digest: 'frame-exact' }))
  const decoded = Strength.storeTryDecodeEnvelope(envelope)
  assert.equal(decoded.ok, true)
  const payload = decoded.value
  assert.equal(payload.budget, 'K1')
  assert.equal('V0' in payload, false)
  assert.equal('V1' in payload, false)
  assert.equal('V2' in payload, false)
  assert.equal('P1' in payload, false)
  assert.equal('P2' in payload, false)
  assert.equal('estimate' in payload, false)
  assert.equal('prediction' in payload, false)
})
}
