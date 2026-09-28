import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
const exchange = (toolName, canonicalArguments, canonicalResult) => ({ toolName, canonicalArguments, canonicalResult })
const batch = (requestOrdinal, exchanges) => ({ requestOrdinal, exchanges })

test('WHAT[speculative-investigation-005] STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches', () => {
  const good = Strength.frameTryBuild(H, [
    batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('grep', '{"pattern":"x"}', 'a:1:x')]),
    batch(2, [exchange('glob', '{"pattern":"**/*.fs"}', 'a.fs')]),
  ])
  assert.equal(good.ok, true)
  assert.equal(good.value.batches.length, 2)
  assert.match(good.value.digest, /^H\(/)
  assert.ok(good.value.byteLength > 0)
  const write = Strength.frameTryBuild(H, [batch(1, [exchange('write', '{}', 'ok')])])
  assert.equal(write.ok, false)
  assert.equal(write.error, 'UnsupportedTool')
  const empty = Strength.frameTryBuild(H, [batch(1, [])])
  assert.equal(empty.ok, false)
  assert.equal(empty.error, 'EmptyBatch')
})
test('WHAT[speculative-investigation-005] STRENGTH_005_frame_digest_and_owner_wire_ids_are_restart_stable', () => {
  const batches = [batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])]
  const first = Strength.frameTryBuild(H, batches).value
  const second = Strength.frameTryBuild(H, batches).value
  assert.equal(first.digest, second.digest)
  const id1 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const id2 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const changed = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 2, first.digest)
  assert.equal(id1, id2)
  assert.notEqual(id1, changed)
  assert.doesNotMatch(id1, /time|guid|random/i)
})
test('WHAT[speculative-investigation-005] STRENGTH_005_repeated_identical_batches_are_two_batches_not_one', () => {
  const built = Strength.frameTryBuild(H, [
    batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')]),
    batch(2, [exchange('read', '{"filePath":"a"}', 'alpha')]),
  ])
  assert.equal(built.ok, true)
  assert.equal(built.value.batches.length, 2)
  assert.equal(built.value.batches[1].exchanges[0].canonicalResult, 'alpha')
  // The digest binds batch boundaries and ordinals, so duplicated content in
  // two requests stays two exchanges rather than collapsing into one.
  const collapsed = Strength.frameTryBuild(H, [batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('read', '{"filePath":"a"}', 'alpha')])])
  assert.notEqual(collapsed.value.digest, built.value.digest)
})
test('WHAT[speculative-investigation-005] STRENGTH_005_media_mirror_fails_closed_instead_of_reconstructing_from_digest', async () => {
  const Adapter = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js")
  const text = (value) => ({ kind: 'text', text: value })
  const media = (mediaType, contentDigest) => ({ kind: 'media', mediaType, contentDigest })
  const msg = (role, parts) => ({ role, parts })
  const rendered = (messages) => ({ messages, hostMessageIds: messages.map(() => null), hostIsPhysical: messages.map(() => false) })
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, rendered([msg('user', [media('image/png', 'digest-only')])]))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})

const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })

// DELEGATE 14.1 / WHAT[005]+[011]: an oversized complete exchange carries no
// length gate: it is admitted as one real round, survives the frame build, and
// never trips the process fuse. The live production fuse path itself belongs to
// the canary layer; this watches the same scope through the unit surface.
test('WHAT[speculative-investigation-005] STRENGTH_005_oversized_complete_exchange_never_trips_a_process_fuse', async () => {
  const huge = 'x'.repeat(70000)
  const runtime = Strength.runtimeCreate()
  const oversizeBinding = () =>
    Strength.runtimeBinding('owner-oversize', 'replica-oversize', 'decision-oversize', 'run-oversize', 'Engineer', 1, 'sem-oversize', [])
  assert.equal(Strength.runtimeRegister(runtime, oversizeBinding()).ok, true)

  const scope = Strength.scopeCreate()
  try {
    assert.equal(Strength.scopeRuntimeRegister(scope, oversizeBinding()).ok, true)

    const outcome = await Strength.transformApply(H, runtime, { messages: [
      user('u1', 'replica-oversize', [hostText('Continue.')]),
      assistant('a1', 'replica-oversize', [hostResult('c1', 'read', { filePath: 'huge' }, huge)]),
    ] }, true)
    assert.equal(outcome.kind, 'Ready', 'an oversized complete exchange is admitted as one real round')
    assert.equal(outcome.batches[0].exchanges[0].canonicalResult, huge, 'the oversized result survives verbatim')

    const built = Strength.frameTryBuild(H, [
      { requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"huge"}', canonicalResult: huge }] },
    ])
    assert.equal(built.ok, true, 'an oversized bundle builds without any Delegate-specific byte ceiling')
    assert.ok(built.value.byteLength > 65536)

    assert.equal(Strength.scopeFuseReason(scope), null, 'oversized material never trips the process fuse')
  } finally {
    Strength.scopeDispose(scope)
  }
})
}


{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { createLocalEventStore } = await import("../../verification-system/tests/support/local-event-store.mjs");

const H = (text) => createHash('sha256').update(text).digest('hex')

// A persisted frame payload is the Store wire contract (encodeFrameBundlePayload):
// version + digest + byte_length + batches[request_ordinal/exchanges[tool_name/arguments/result]].
// The JS bundle shape (batches/byteLength/requestOrdinal/toolName/...) is the Surface
// shape, not the payload shape; serializing it verbatim makes decode refuse the load.
const storeWirePayload = (bundle, overrides = {}) => ({
  version: 1,
  digest: bundle.digest,
  byte_length: bundle.byteLength,
  batches: bundle.batches.map((b) => ({
    request_ordinal: b.requestOrdinal,
    exchanges: b.exchanges.map((e) => ({ tool_name: e.toolName, arguments: e.canonicalArguments, result: e.canonicalResult })),
  })),
  ...overrides,
})

// WHAT[005]: there is no Delegate-owned byte ceiling. An exchange larger than
// any historical limit still builds, persists, reloads and renders whole.
test('WHAT[speculative-investigation-005] STRENGTH_005_oversized_complete_exchange_survives_build_persist_load_and_render', async () => {
  const huge = 'x'.repeat(70000)
  const built = Strength.frameTryBuild(H, [
    { requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"huge"}', canonicalResult: huge }] },
    { requestOrdinal: 2, exchanges: [{ toolName: 'grep', canonicalArguments: '{"pattern":"huge"}', canonicalResult: 'huge:1' }] },
  ])
  assert.equal(built.ok, true)
  const bundle = built.value
  assert.ok(bundle.byteLength > 65536, 'the bundle must not be capped at any historical frame limit')
  assert.match(bundle.digest, /^[0-9a-f]{64}$/)

  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const decision = 'decision-oversize'
    const ref = await Strength.storeWritePayload(local.store, new TextEncoder().encode(JSON.stringify(storeWirePayload(bundle))))
    assert.equal(ref.ok, true)
    for (const event of [
      Strength.eventRequested({
        decisionId: decision, ownerSessionId: 'owner',
        ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
        sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
        sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
      }),
      Strength.eventBound(decision, 'run-1', 'replica-oversize', 'anchor-1'),
      Strength.eventPrepared('owner', decision, 'run-1', 'replica-oversize', 'anchor-1', bundle.digest, bundle.byteLength, [ref.value]),
    ]) {
      const appended = await Strength.durabilityAppend(durability, event)
      assert.equal(appended.ok, true, appended.error)
    }

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    assert.equal(Strength.projectionRequestedRounds(decision, projection), 2)
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, decision)
    assert.equal(loaded.ok, true)
    assert.equal(loaded.value.byteLength, bundle.byteLength)
    assert.equal(loaded.value.digest, bundle.digest)

    const intent = Strength.candidate(H, {
      ownerSessionId: 'owner', decisionId: decision, targetProviderRun: 'run-1', currentProviderRun: 'run-1', bundle: loaded.value,
    })
    assert.equal(intent.ok, true, intent.error)
  } finally { local.close() }
})

// WHAT[005]: digest and byte length are the integrity proof of a persisted frame
// payload. Tampering with either one must be refused through the real decode and
// load path, never accepted as a half-valid bundle.
test('WHAT[speculative-investigation-005] STRENGTH_005_tampered_digest_or_byte_length_is_refused', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = Strength.frameTryBuild(H, [
      { requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }] },
    ]).value

    const loadWithPayload = async (decision, run, callId, overrides) => {
      const ref = await Strength.storeWritePayload(local.store, new TextEncoder().encode(JSON.stringify(storeWirePayload(bundle, overrides))))
      assert.equal(ref.ok, true)
      const requested = await Strength.durabilityAppend(durability, Strength.eventRequested({
        decisionId: decision, ownerSessionId: 'owner',
        ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
        sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: run,
        sourceToolCallIds: [callId], requestedRounds: 1, contractRevision: 1,
      }))
      assert.equal(requested.ok, true, requested.error)
      const bound = await Strength.durabilityAppend(durability, Strength.eventBound(decision, run, `replica-${decision}`, 'anchor-1'))
      assert.equal(bound.ok, true, bound.error)
      const prepared = await Strength.durabilityAppend(durability, Strength.eventPrepared('owner', decision, run, `replica-${decision}`, 'anchor-1', bundle.digest, bundle.byteLength, [ref.value]))
      assert.equal(prepared.ok, true, prepared.error)
      const projection = (await Strength.durabilityLoadProjection(durability)).value
      return await Strength.durabilityLoadBundleForDecision(durability, projection, decision)
    }

    const tamperedDigest = await loadWithPayload('dec-tamper-digest', 'run-1', 'call-1', { digest: 'swapped' })
    assert.equal(tamperedDigest.ok, false)
    assert.match(tamperedDigest.error, /digest\/length/, 'a tampered digest must be refused by the integrity check')

    const tamperedLength = await loadWithPayload('dec-tamper-length', 'run-2', 'call-2', { byte_length: bundle.byteLength + 1 })
    assert.equal(tamperedLength.ok, false)
    assert.match(tamperedLength.error, /digest\/length/, 'a tampered byte length must be refused by the integrity check')

    // The honest payload still loads: the refusals above are the integrity check
    // doing its job, not a blanket rejection of the load path.
    const honest = await loadWithPayload('dec-honest', 'run-3', 'call-3', {})
    assert.equal(honest.ok, true, honest.error)
    assert.equal(honest.value.digest, bundle.digest)
    assert.equal(honest.value.byteLength, bundle.byteLength)
  } finally { local.close() }
})

// WHAT[005]: the Frame keeps exactly what the tool returned to the model, host
// truncation marker included. Nothing reconstructs the removed full text.
test('WHAT[speculative-investigation-005] STRENGTH_005_natural_tool_truncation_survives_build_persist_load_and_render', async () => {
  const truncatedResult = '[tool output truncated to the last 2000 lines]\n' + 'tail line\n'.repeat(2000)
  const built = Strength.frameTryBuild(H, [
    { requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"build.log"}', canonicalResult: truncatedResult }] },
  ])
  assert.equal(built.ok, true)
  const bundle = built.value

  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const decision = 'decision-truncated'
    const run = 'run-9'
    const ref = await Strength.storeWritePayload(local.store, new TextEncoder().encode(JSON.stringify(storeWirePayload(bundle))))
    assert.equal(ref.ok, true)
    const requested = await Strength.durabilityAppend(durability, Strength.eventRequested({
      decisionId: decision, ownerSessionId: 'owner',
      ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
      sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: run,
      sourceToolCallIds: ['call-9'], requestedRounds: 1, contractRevision: 1,
    }))
    assert.equal(requested.ok, true, requested.error)
    const bound = await Strength.durabilityAppend(durability, Strength.eventBound(decision, run, 'replica-truncated', 'anchor-9'))
    assert.equal(bound.ok, true, bound.error)
    const prepared = await Strength.durabilityAppend(durability, Strength.eventPrepared('owner', decision, run, 'replica-truncated', 'anchor-9', bundle.digest, bundle.byteLength, [ref.value]))
    assert.equal(prepared.ok, true, prepared.error)

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, decision)
    assert.equal(loaded.ok, true, loaded.error)
    assert.equal(
      loaded.value.batches[0].exchanges[0].canonicalResult,
      truncatedResult,
      'the frame must replay the truncated result exactly as returned, not a reconstructed full text',
    )
    assert.equal(loaded.value.byteLength, bundle.byteLength)
    assert.equal(loaded.value.digest, bundle.digest)

    const intent = Strength.candidate(H, {
      ownerSessionId: 'owner', decisionId: decision, targetProviderRun: run, currentProviderRun: run, bundle: loaded.value,
    })
    assert.equal(intent.ok, true, intent.error)

    // The digest binds the exact returned bytes: re-creating the "full" text by
    // appending reconstructed content yields a different digest, so a frame can
    // never silently swap the real result for a padded one.
    const padded = Strength.frameTryBuild(H, [
      { requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"build.log"}', canonicalResult: truncatedResult + 'reconstructed tail' }] },
    ])
    assert.equal(padded.ok, true)
    assert.notEqual(padded.value.digest, bundle.digest)
  } finally { local.close() }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const text = (textValue) => ({ kind: 'text', text: textValue })
const message = (role, parts) => ({ role, parts })
const snapshot = (messages) => Projection.projectionSnapshot(Projection.semanticProjection(messages))

test('WHAT[speculative-investigation-005] STRENGTH_005_009_candidate_renders_concurrent_calls_then_results_with_stable_ids', () => {
  const bundle = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [
    { toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' },
    { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' },
  ] }]).value
  const base = [message('user', [text('base')])]
  const intent = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target', currentProviderRun: 'target', bundle }).value
  const first = Projection.renderMessagesWithHostIds(snapshot(base), base, [intent])
  const second = Projection.renderMessagesWithHostIds(snapshot(base), base, [intent])
  assert.equal(first.messages.length, 3)
  assert.deepEqual(first.messages.map((item) => item.role), ['user', 'assistant', 'tool'])
  const calls = first.messages[1].parts
  const results = first.messages[2].parts
  assert.deepEqual(calls.map((part) => part.kind), ['tool-call', 'tool-call'])
  assert.deepEqual(results.map((part) => part.kind), ['tool-result', 'tool-result'])
  assert.deepEqual(calls.map((part) => part.callId), results.map((part) => part.callId))
  assert.equal(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
})
test('WHAT[speculative-investigation-005] STRENGTH_005_candidate_refuses_wrong_target_and_tampered_bundle', () => {
  const bundle = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }] }]).value
  const wrongTarget = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', currentProviderRun: 'target-b', bundle })
  assert.equal(wrongTarget.ok, false)
  assert.equal(wrongTarget.error, 'StrengthCandidateWrongTarget')
  const tampered = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', currentProviderRun: 'target-a', bundle: { ...bundle, digest: 'tampered' } })
  assert.equal(tampered.ok, false)
  assert.equal(tampered.error, 'StrengthFrameDigestMismatch')
})
}
