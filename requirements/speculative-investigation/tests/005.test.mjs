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
  const good = Strength.frameTryBuild(H, 10000, [
    batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('grep', '{"pattern":"x"}', 'a:1:x')]),
    batch(2, [exchange('glob', '{"pattern":"**/*.fs"}', 'a.fs')]),
  ])
  assert.equal(good.ok, true)
  assert.equal(good.value.batches.length, 2)
  assert.match(good.value.digest, /^H\(/)
  assert.ok(good.value.byteLength > 0)
  const write = Strength.frameTryBuild(H, 10000, [batch(1, [exchange('write', '{}', 'ok')])])
  assert.equal(write.ok, false)
  assert.equal(write.error, 'UnsupportedTool')
  const empty = Strength.frameTryBuild(H, 10000, [batch(1, [])])
  assert.equal(empty.ok, false)
  assert.equal(empty.error, 'EmptyBatch')
})
test('WHAT[speculative-investigation-005] repeated frame construction and owner wire ID derivation are deterministic', () => {
  const batches = [batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])]
  const first = Strength.frameTryBuild(H, 10000, batches).value
  const second = Strength.frameTryBuild(H, 10000, batches).value
  assert.equal(first.digest, second.digest)
  const id1 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const id2 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const changed = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 2, first.digest)
  assert.equal(id1, id2)
  assert.notEqual(id1, changed)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Adapter = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const text = (value) => ({ kind: 'text', text: value })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const media = (mediaType, contentDigest) => ({ kind: 'media', mediaType, contentDigest })
const msg = (role, parts) => ({ role, parts })
const rendered = (messages) => ({ messages, hostMessageIds: messages.map(() => null), hostIsPhysical: messages.map(() => false) })

test('WHAT[speculative-investigation-005] STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest', () => {
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, rendered([msg('user', [media('image/png', 'digest-only')])]))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const text = (textValue) => ({ kind: 'text', text: textValue })
const message = (role, parts) => ({ role, parts })

test('WHAT[speculative-investigation-005] STRENGTH_005_009_candidate_renders_concurrent_calls_then_results_with_stable_ids', () => {
  const base = [message('user', [text('base')])]
  const intent = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target', currentProviderRun: 'target', bundle }).value
  const first = Projection.renderMessagesWithHostIds(snapshot(base), base, [intent])
  const second = Projection.renderMessagesWithHostIds(snapshot(base), base, [intent])
  assert.equal(first.messages.length, 3)
  assert.equal(first.messages[0].role, 'user')
  assert.equal(first.messages[1].role, 'assistant')
  assert.equal(first.messages[2].role, 'tool')
  const calls = first.messages[1].parts
  const results = first.messages[2].parts
  assert.deepEqual(calls.map((part) => part.kind), ['tool-call', 'tool-call'])
  assert.deepEqual(results.map((part) => part.kind), ['tool-result', 'tool-result'])
  assert.deepEqual(calls.map((part) => part.callId), results.map((part) => part.callId))
  assert.equal(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
})
}

{
const assert = (await import('node:assert/strict')).default
const Strength = await import('../../../dist/Strength/Surface.js')

test('WHAT[speculative-investigation-005] frame cap counts complete canonical UTF-8 material and rejects the whole bundle below its exact boundary', () => {
  for (const result of ['ASCII', '中文', '😀', '\uD800']) {
    const batches = [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{}', canonicalResult: result }] }]
    let canonical
    const digest = text => { canonical = text; return 'recorded-digest' }
    const built = Strength.frameTryBuild(digest, 65536, batches)
    assert.equal(built.ok, true)
    const expectedBytes = Buffer.byteLength(canonical, 'utf8')
    assert.equal(built.value.byteLength, expectedBytes)
    assert.ok(expectedBytes > Buffer.byteLength(result, 'utf8'))
    assert.equal(Strength.frameTryBuild(digest, expectedBytes, batches).ok, true)
    const rejected = Strength.frameTryBuild(digest, expectedBytes - 1, batches)
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error, 'ByteLimitExceeded')
  }
})

test('WHAT[speculative-investigation-005] request ordinals must be contiguous and every owner identity component changes the derived call identity', () => {
  const exchange = { toolName: 'read', canonicalArguments: '{}', canonicalResult: 'x' }
  for (const ordinals of [[0], [2], [1, 1], [1, 3]]) {
    assert.equal(Strength.frameTryBuild(text => text, 65536, ordinals.map(requestOrdinal => ({ requestOrdinal, exchanges: [exchange] }))).error, 'InvalidRequestOrdinal')
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
