import assert from 'node:assert/strict'
import test from 'node:test'
import * as Adapter from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`
const text = (value) => ({ kind: 'text', text: value })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const media = (mediaType, contentDigest) => ({ kind: 'media', mediaType, contentDigest })
const msg = (role, parts) => ({ role, parts })
const rendered = (messages) => ({ messages, hostMessageIds: messages.map(() => null), hostIsPhysical: messages.map(() => false) })

test('WHAT[SPEC-INV-009] STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids', () => {
  const input = rendered([msg('user', [text('hello')]), msg('assistant', [text('world')])])
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, input)
  assert.equal(applied.ok, true)
  assert.equal(applied.value.length, 2)
  assert.equal(applied.value[0].info.sessionID, 'replica-session')
  assert.doesNotMatch(applied.value[0].info.id, /strength|replica|prefetch/i)
  const decoded = Adapter.decodeMessageView(applied.value)
  assert.equal(Projection.renderWire(decoded.messages), Projection.renderWire(input.messages))
})

test('WHAT[SPEC-INV-009] STRENGTH_009_host_adapter_encodes_strength_tool_pairs_as_native_completed_OpenCode_parts', () => {
  const input = {
    messages: [msg('user', [text('owner mirror')]), msg('assistant', [call('c1', 'read', '{"filePath":"README.md"}'), call('c2', 'grep', '{"pattern":"Strength"}')]), msg('tool', [result('c1', 'alpha'), result('c2', 'beta')])],
    hostMessageIds: [null, 'synthetic-call-message', 'synthetic-result-message'],
    hostIsPhysical: [false, false, false],
  }
  const applied = Strength.tryApplyRenderedMessages('replica-session', H, input)
  assert.equal(applied.ok, true)
  assert.equal(applied.value.length, 2)
  assert.equal(applied.value[1].info.role, 'assistant')
  assert.deepEqual(applied.value[1].parts.map((part) => part.type), ['tool', 'tool'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.callID), ['c1', 'c2'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.tool), ['read', 'grep'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.status), ['completed', 'completed'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.input), [{ filePath: 'README.md' }, { pattern: 'Strength' }])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.output), ['alpha', 'beta'])
})

test('WHAT[SPEC-INV-005] STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest', () => {
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, rendered([msg('user', [media('image/png', 'digest-only')])]))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})
