import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as Adapter from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const H = (text) => `H(${text})`
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const exchange = (toolName, canonicalArguments, canonicalResult) => ({ toolName, canonicalArguments, canonicalResult })
const batch = (requestOrdinal, exchanges) => ({ requestOrdinal, exchanges })
const text = (value) => ({ kind: 'text', text: value })
const media = (mediaType, contentDigest) => ({ kind: 'media', mediaType, contentDigest })
const msg = (role, parts) => ({ role, parts })
const rendered = (messages) => ({ messages, hostMessageIds: messages.map(() => null), hostIsPhysical: messages.map(() => false) })
const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const message = (role, parts) => ({ role, parts })

test('WHAT[SPEC-INV-005] STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches', () => {
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

test('WHAT[SPEC-INV-005] STRENGTH_005_frame_digest_and_owner_wire_ids_are_restart_stable', () => {
  const batches = [batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])]
  const first = Strength.frameTryBuild(H, 10000, batches).value
  const second = Strength.frameTryBuild(H, 10000, batches).value
  assert.equal(first.digest, second.digest)
  const id1 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const id2 = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 1, first.digest)
  const changed = Strength.frameWireToolCallId(H, 'owner', 'd1', 1, 2, first.digest)
  assert.equal(id1, id2)
  assert.notEqual(id1, changed)
  assert.doesNotMatch(id1, /time|guid|random/i)
})

test('WHAT[SPEC-INV-005] STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest', () => {
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, rendered([msg('user', [media('image/png', 'digest-only')])]))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})

test('WHAT[SPEC-INV-005] STRENGTH_005_009_candidate_renders_concurrent_calls_then_results_with_stable_ids', () => {
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
