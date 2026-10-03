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

test('WHAT[speculative-investigation-005] predictor text crosses as reasoning without its native reasoning', async () => {
  const texts = [' 核对结果\n𠀀 ', '第二段', '结束正文']
  const messages = [
    { role: 'assistant', parts: [
      { kind: 'reasoning', text: 'private thinking' },
      { kind: 'text', text: texts[0] },
      { kind: 'text', text: texts[1] },
      call('p1', 'js-predictor', '{"program":"read"}'),
    ] },
    { role: 'tool', parts: [result('p1', 'actual evidence')] },
    { role: 'assistant', parts: [
      { kind: 'reasoning', text: 'private terminal thinking' },
      { kind: 'text', text: texts[2] },
    ] },
  ]
  const batches = Strength.collectCompleteBatches(messages)
  assert.deepEqual(batches, [
    { requestOrdinal: 1, assistantText: texts.slice(0, 2), exchanges: [
      exchange('js-predictor', '{"program":"read"}', 'actual evidence'),
    ] },
    { requestOrdinal: 2, assistantText: texts.slice(2), exchanges: [] },
  ])
  const built = Strength.frameTryBuild(H, batches)
  assert.equal(built.ok, true, built.error)
  for (const intent of [
    Strength.candidate(H, {
      ownerSessionId: 'owner', ownerRole: 'engineer', decisionId: 'text-delivery',
      targetProviderRun: 'target', currentProviderRun: 'target', bundle: built.value,
    }),
    Strength.promoted(H, {
      ownerSessionId: 'owner', ownerRole: 'engineer', decisionId: 'text-delivery',
      beforeIndex: 0, isReplicaRequest: false, bundle: built.value,
    }),
  ]) {
    assert.equal(intent.ok, true, intent.error)
    const Projection = await import('../../../dist/Participant/Provider/Projection/Surface.js')
    const rendered = Projection.renderMessages(Projection.projectionSnapshot(Projection.semanticProjection([])), [], [intent.value])
    assert.deepEqual(rendered.flatMap(message => message.parts).filter(part => part.kind === 'reasoning'),
      texts.map(text => ({ kind: 'reasoning', text })))
    assert.equal(rendered.flatMap(message => message.parts).some(part => part.kind === 'text'), false)
    const encoded = Strength.tryApplyRenderedMessages('owner', H, {
      messages: rendered, hostMessageIds: rendered.map(() => null), hostIsPhysical: rendered.map(() => false),
    })
    assert.equal(encoded.ok, true, encoded.error)
    assert.deepEqual(encoded.value.flatMap(message => message.parts).filter(part => part.type === 'reasoning').map(part => part.text), texts)
  }
  assert.deepEqual(Strength.collectCompleteBatches([
    { role: 'assistant', parts: [{ kind: 'reasoning', text: 'private only' }] },
  ]), [])
  assert.deepEqual(Strength.collectCompleteBatches([
    { role: 'assistant', parts: [{ kind: 'text', text: 'unfinished' }, call('pending', 'read', '{}')] },
  ]), [], 'incomplete tool batches do not publish their accompanying text')
})

test('WHAT[speculative-investigation-005] STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches', () => {
  const good = Strength.frameTryBuild(H, [
    batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('grep', '{"pattern":"x"}', 'a:1:x')]),
    batch(2, [exchange('glob', '{"pattern":"**/*.fs"}', 'a.fs')]),
  ])
  assert.equal(good.ok, true)
  assert.equal(good.value.batches.length, 2)
  assert.match(good.value.digest, /^H\(/)
  assert.ok(good.value.byteLength > 0)
  const withJsPredictor = Strength.frameTryBuild(H, [
    batch(1, [exchange('js-predictor', '{"program":"..."}', 'readonly result')]),
  ])
  assert.equal(withJsPredictor.ok, true)
  const writeOnly = Strength.frameTryBuild(H, [batch(1, [exchange('write', '{}', 'ok')])])
  assert.equal(writeOnly.ok, false)
  assert.equal(writeOnly.error, 'UnsupportedTool')
  const empty = Strength.frameTryBuild(H, [batch(1, [])])
  assert.equal(empty.ok, false)
  assert.equal(empty.error, 'EmptyBatch')
})
test('WHAT[speculative-investigation-005] host_completed_tool_part_message_builds_a_frame_instead_of_orphan_failure', async () => {
  // The real Host session shape: ONE assistant message whose parts are a
  // completed `tool` part beside non-tool parts. ProviderWireDecode decodes such
  // a part as a result only, so the message reaches the adapter with results and
  // no preceding call batch. It must become a built exchange, not a retire.
  const { default: crypto } = await import('node:crypto')
  const Transform = await import('../../../dist/Strength/Surface.js')
  const Wire = await import('../../../dist/OpenCode/Codec/ProviderProjectionSurface.js')
  const sha256 = (text) => crypto.createHash('sha256').update(text).digest('hex')
  const runtime = Transform.runtimeCreate()
  assert.equal(Transform.runtimeRegister(runtime, Transform.runtimeBinding('owner', 'replica', 'dec', 'run', 'devops', 2, 'sd', [])).ok, true)
  const messages = [{
    info: { role: 'assistant', id: 'msg_src', sessionID: 'replica' },
    parts: [
      { type: 'step-start' },
      { type: 'reasoning', text: 'r' },
      { type: 'text', text: 'read preface' },
      { type: 'tool', callID: 'call_1', tool: 'glob', state: { status: 'completed', input: { pattern: 'x' }, output: 'found 12' } },
      { type: 'step-finish' },
    ],
  }]
  // transformApply replaces `messages` in place, so snapshot the owner's original
  // transcript before the transform; the adapter assertion below must see that
  // pristine Host shape, not the frame rows written back over it.
  const pristineHostMessages = JSON.parse(JSON.stringify(messages))
  const out = await Transform.transformApply(sha256, runtime, { messages }, true)
  assert.equal(out.kind, 'Ready', `expected a built frame, got ${out.kind} ${out.reason ?? ''}`)
  assert.deepEqual(out.batches.flatMap((batch) => batch.exchanges.map((exchange) => exchange.toolName)), ['glob'])
  assert.equal(out.batches[0].exchanges[0].canonicalResult, 'found 12')
  assert.deepEqual(out.batches[0].assistantText, ['read preface'])
  assert.deepEqual(messages.flatMap(message => message.parts).filter(part => part.type === 'text').map(part => part.text),
    ['read preface'], 'predictor keeps its own mixed tool text once, without demoting it')
  assert.deepEqual(messages.flatMap(message => message.parts).filter(part => part.type === 'reasoning').map(part => part.text),
    ['r'], 'native thinking remains in the predictor history, not in the returned material')

  // The mirror the adapter actually receives is the localized owner transcript,
  // and that transcript keeps the result-only assistant message verbatim
  // (Frame.tryLocalizeMirror tolerates it). Passing it through the adapter is the
  // path that used to die in requirePendingBatch, so exercise it directly rather
  // than only through the batch collector above.
  const decoded = Wire.decodeMessageView(pristineHostMessages)
  // ProviderProjectionSurface emits capitalized discriminators ("Reasoning",
  // "ToolResult"); Strength.Surface accepts only its own lowercase vocabulary.
  // Normalize rather than feeding one surface's output into the other's decoder.
  const wireMessages = decoded.messages.map((message) => ({
    role: message.role,
    parts: message.parts.map((part) =>
      part.kind === 'ToolResult'
        ? { kind: 'tool-result', callId: part.callId, result: part.result }
        : part.kind === 'Reasoning'
          ? { kind: 'reasoning', text: part.text }
          : part.kind === 'Text'
            ? { kind: 'text', text: part.text }
          : part),
  }))
  const localized = Transform.frameTryLocalizeMirror(sha256, 'dec', sha256('anchor'), wireMessages)
  assert.equal(localized.ok, true)
  assert.equal(localized.value.length, 1)
  assert.equal(localized.value[0].role, 'assistant')
  assert.deepEqual(localized.value[0].parts.map((part) => part.kind), ['reasoning', 'text', 'tool-result'])
  const rendered = { messages: localized.value, hostMessageIds: [null], hostIsPhysical: [false] }
  const applied = Transform.tryApplyRenderedMessages('replica', sha256, rendered)
  assert.equal(applied.ok, true, `host session-shaped message must be emitted, got ${applied.error}`)
  const appliedParts = applied.value[0].parts
  assert.deepEqual(appliedParts.map((part) => part.type), ['reasoning', 'text', 'tool'])
  const completed = appliedParts.find((part) => part.type === 'tool')
  assert.equal(completed.tool, '')
  assert.equal(completed.state.status, 'completed')
  assert.equal(completed.state.output, 'found 12')
  assert.equal(completed.callID, localized.value[0].parts[2].callId, 'the completed part keeps the relocated call identity')

  // A genuinely orphaned result is still refused: a logical `tool` message carries
  // only the result half, so it must keep requiring its preceding call batch.
  const orphan = Transform.tryApplyRenderedMessages('replica', sha256, {
    messages: [{ role: 'tool', parts: [{ kind: 'tool-result', callId: 'orphan-1', result: 'no call ever arrived' }] }],
    hostMessageIds: [null],
    hostIsPhysical: [false],
  })
  assert.equal(orphan.ok, false)
  assert.match(orphan.error, /without a preceding call batch/)
})

test('WHAT[speculative-investigation-005] repeated frame construction and owner wire ID derivation are deterministic', () => {
  const batches = [batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])]
  const first = Strength.frameTryBuild(H, batches).value
  const second = Strength.frameTryBuild(H, batches).value
  assert.equal(first.digest, second.digest)
  const id1 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const id2 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const changed = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 2, first.digest)
  assert.equal(id1, id2)
  assert.notEqual(id1, changed)
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

{
const assert = (await import('node:assert/strict')).default
const Strength = await import('../../../dist/Strength/Surface.js')

test('WHAT[speculative-investigation-005] frame counts complete canonical UTF-8 material without delegate byte ceiling', () => {
  for (const result of ['ASCII', '中文', '😀', '\uD800']) {
    const batches = [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{}', canonicalResult: result }] }]
    let canonical
    const digest = text => { canonical = text; return 'recorded-digest' }
    const built = Strength.frameTryBuild(digest, batches)
    assert.equal(built.ok, true)
    const expectedBytes = Buffer.byteLength(canonical, 'utf8')
    assert.equal(built.value.byteLength, expectedBytes)
    assert.ok(expectedBytes > Buffer.byteLength(result, 'utf8'))
  }
})

test('WHAT[speculative-investigation-005] request ordinals must be contiguous and every owner identity component changes the derived call identity', () => {
  const exchange = { toolName: 'read', canonicalArguments: '{}', canonicalResult: 'x' }
  for (const ordinals of [[0], [2], [1, 1], [1, 3]]) {
    assert.equal(Strength.frameTryBuild(text => text, ordinals.map(requestOrdinal => ({ requestOrdinal, exchanges: [exchange] }))).error, 'InvalidRequestOrdinal')
  }
  const base = ['owner', 'decision', 1, 1, 'digest']
  const id = Strength.frameWireToolCallId(text => text, ...base)
  for (const [index, changed] of ['other-owner', 'other-decision', 2, 2, 'other-digest'].entries()) {
    const alternate = [...base]
    alternate[index] = changed
    assert.notEqual(Strength.frameWireToolCallId(text => text, ...alternate), id)
  }
})
}
