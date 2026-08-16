import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
const exchange = (toolName, canonicalArguments, canonicalResult) => ({ toolName, canonicalArguments, canonicalResult })
const batch = (requestOrdinal, exchanges) => ({ requestOrdinal, exchanges })

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

test('WHAT[SPEC-INV-009] STRENGTH_009_replica_mirror_localizes_owner_call_ids_without_changing_semantics', () => {
  const ownerMessages = [
    { role: 'assistant', parts: [call('owner-a', 'read', '{"filePath":"a"}'), call('owner-b', 'grep', '{"pattern":"x"}')] },
    { role: 'tool', parts: [result('owner-b', 'hit'), result('owner-a', 'alpha')] },
  ]
  const digest = H(Strength.renderSemantic(ownerMessages))
  const first = Strength.frameTryLocalizeMirror(H, 'd1', digest, ownerMessages)
  const second = Strength.frameTryLocalizeMirror(H, 'd1', digest, ownerMessages)
  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.equal(Strength.renderSemantic(first.value), Strength.renderSemantic(ownerMessages))
  assert.equal(Strength.renderWire(first.value), Strength.renderWire(second.value))
  assert.doesNotMatch(Strength.renderWire(first.value), /owner-a|owner-b/)
  const localizedCalls = first.value[0].parts.filter((part) => part.kind === 'tool-call').map((part) => part.callId)
  const localizedResults = first.value[1].parts.filter((part) => part.kind === 'tool-result').map((part) => part.callId)
  assert.deepEqual(localizedResults, [localizedCalls[1], localizedCalls[0]])
  const orphan = Strength.frameTryLocalizeMirror(H, 'd2', digest, [{ role: 'tool', parts: [result('missing', 'no-call')] }])
  assert.equal(orphan.ok, false)
  assert.equal(orphan.error, 'OrphanToolResultId')
  const media = Strength.frameTryLocalizeMirror(H, 'd3', digest, [{ role: 'user', parts: [{ kind: 'media', mediaType: null, contentDigest: 'digest' }] }])
  assert.equal(media.ok, false)
  assert.equal(media.error, 'MediaCannotCrossSession')
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
