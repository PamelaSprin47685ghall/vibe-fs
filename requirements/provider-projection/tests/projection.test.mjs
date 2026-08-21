// Host message/request wire projection through provider owner surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as wire from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const kind = (value) => value?.kind

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_part_text_reasoning', () => {
  assert.equal(wire.decodeWirePart(null), null)
  assert.deepEqual(wire.decodeWirePart({ type: 'text', text: 'hi' }), { kind: 'Text', text: 'hi' })
  assert.equal(wire.decodeWirePart({ type: 'text' }), null)
  assert.deepEqual(wire.decodeWirePart({ type: 'reasoning', reasoning: 'r' }), { kind: 'Reasoning', text: 'r' })
  assert.deepEqual(wire.decodeWirePart({ type: 'thinking', text: 't' }), { kind: 'Reasoning', text: 't' })
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_part_tool_call_states', () => {
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-call', callID: 'c1', name: 'bash', state: { status: 'completed', output: { ok: 1 } } }), { kind: 'ToolResult', callId: 'c1', result: '{"ok":1}' })
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-call', callId: 'c2', tool: 'bash', state: { status: 'error', errorText: 'bad' } }), { kind: 'ToolResult', callId: 'c2', result: 'bad' })
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-call', callID: 'c3', name: 'bash', state: { status: 'running' }, arguments: { x: 1 } }), { kind: 'ToolCall', callId: 'c3', name: 'bash', args: '{"x":1}' })
  assert.deepEqual(wire.decodeWirePart({ type: 'tool', id: 'c4', name: 'read', args: 'raw' }), { kind: 'ToolCall', callId: 'c4', name: 'read', args: 'raw' })
  assert.equal(wire.decodeWirePart({ type: 'tool-call', id: 'c5' }), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_part_tool_result_and_tool_prefix', () => {
  assert.deepEqual(wire.decodeWirePart({ type: 'tool_result', callID: 'c1', result: { ok: true } }), { kind: 'ToolResult', callId: 'c1', result: '{"ok":true}' })
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-result', id: 'c2', output: 'out' }), { kind: 'ToolResult', callId: 'c2', result: 'out' })
  assert.equal(wire.decodeWirePart({ type: 'tool_result', result: 'x' }), null)
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-output', toolCallId: 't1', output: 'done' }), { kind: 'ToolResult', callId: 't1', result: 'done' })
  assert.deepEqual(wire.decodeWirePart({ type: 'tool-error', callID: 't2', errorText: 'nope' }), { kind: 'ToolResult', callId: 't2', result: 'nope' })
  assert.equal(wire.decodeWirePart({ type: 'tool-output', output: 'x' }), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_part_file_media', () => {
  const media = wire.decodeWirePart({ type: 'file', url: 'https://x/y.png', mime: 'image/png' })
  assert.equal(kind(media), 'Media')
  assert.equal(media.mediaType, 'image/png')
  assert.match(media.contentDigest, /^[0-9a-f]{64}$/)
  assert.equal(wire.decodeWirePart({ type: 'file' }), null)
  assert.equal(wire.decodeWirePart({ type: 'mystery' }), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_message_and_request', () => {
  assert.equal(wire.decodeMessage(null), null)
  const msg = wire.decodeMessage({ role: 'user', parts: [{ type: 'text', text: 'a' }, { type: 'patch' }, { type: 'text', text: 'b' }] })
  assert.equal(msg.role, 'user')
  assert.equal(msg.parts.length, 2)
  assert.equal(wire.decodeMessage({ info: { role: 'assistant' }, parts: [{ type: 'text', text: 'x' }] }).role, 'assistant')
  assert.equal(wire.decodeMessage({ role: ' ', parts: [] }), null)
  const req = wire.decodeRequest({ model: { providerID: 'p', modelID: 'm', variant: 'v' }, tools: [{ function: { name: 'fn' } }, { name: 'plain' }], system: ['sys1', null, 'sys2'], messages: [{ role: 'user', parts: [{ type: 'text', text: 'hi' }] }] })
  assert.equal(req.providerId, 'p')
  assert.equal(req.modelId, 'm')
  assert.equal(req.variant, 'v')
  assert.deepEqual(req.tools, ['fn', 'plain'])
  assert.deepEqual(req.system, ['sys1', 'sys2'])
  assert.equal(req.messages.length, 1)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_decode_request_falls_back_to_camel_and_id', () => {
  const req = wire.decodeRequest({ model: { providerId: 'p2', modelId: 'm2' }, tools: [{ name: 't' }], system: [], messages: [] })
  assert.equal(req.providerId, 'p2')
  assert.equal(req.modelId, 'm2')
  assert.equal(req.variant, null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_message_view_and_transform_output', () => {
  const view = wire.decodeMessageView([{ role: 'user', parts: [{ type: 'text', text: 'q' }] }])
  assert.equal(view.providerId, null)
  assert.deepEqual(view.tools, [])
  assert.equal(view.messages.length, 1)
  assert.deepEqual(wire.messagesFromTransformOutput({ messages: [{ role: 'user' }] }), [{ role: 'user' }])
})

test('WHAT[PROVIDER-PROJECTION-004] MISC_projection_prepend_companion_memory', () => {
  const raw = [{ info: { id: 'm1' }, role: 'user' }, { info: { id: 'm2' }, role: 'user' }]
  const prefixed = wire.prependCompanionMemory(raw, 'syn-1', 'remember this', 1)
  assert.equal(prefixed.length, 2)
  assert.equal(prefixed[0].info.id, 'syn-1')
  assert.equal(prefixed[0].parts[0].text, 'remember this')
  assert.equal(prefixed[1].info.id, 'm2')
  assert.throws(() => wire.prependCompanionMemory(raw, 's', 'm', 5), /cutoff exceeds/)
})

test('WHAT[PROVIDER-PROJECTION-004] MISC_projection_y_prefix_preserves_raw_todowrite_rounds', () => {
  const message = (id, role, parts) => ({ info: { id, role }, parts })
  const raw = [
    message('u0', 'user', [{ type: 'text', text: 'ordinary old context' }]),
    message('a0', 'assistant', [
      { type: 'text', text: 'updating obligations' },
      { type: 'tool-call', tool: 'todowrite', callID: 'todo-call-1', args: { planComplete: false } },
    ]),
    message('t0', 'tool', [{ type: 'tool-result', callID: 'todo-call-1', result: { ok: true } }]),
    message('a1', 'assistant', [{ type: 'text', text: 'ordinary replaced tail' }]),
    message('u1', 'user', [{ type: 'text', text: 'live request' }]),
  ]

  const projected = wire.prependCompanionMemory(raw, 'y-prefix', 'compressed ordinary history', 4)
  assert.deepEqual(projected.map(item => item.info.id), ['y-prefix', 'a0', 't0', 'u1'])
  assert.equal(projected[1], raw[1], 'todowrite call message remains the exact raw Host object')
  assert.equal(projected[2], raw[2], 'matching result message remains the exact raw Host object')
})

test('WHAT[PROVIDER-PROJECTION-004] Y_prefix_removes_covered_history_by_stable_Host_identity_not_request_local_index', () => {
  const message = (id, role, text) => ({
    info: { id, role },
    parts: [{ type: 'text', text }],
  })
  const raw = [
    message('covered-u', 'user', 'old user'),
    message('request-local', 'assistant', 'request-local presentation only'),
    message('covered-a', 'assistant', 'old answer'),
    message('live-u', 'user', 'live request'),
  ]

  const projected = wire.prependCompanionMemoryByHostIds(
    raw,
    'y-prefix',
    'compressed canonical X',
    ['covered-u', 'covered-a'],
    '',
  )

  assert.deepEqual(
    projected.map(item => item.info.id),
    ['y-prefix', 'request-local', 'live-u'],
    'an unrelated presentation row must not move the replacement boundary',
  )
  assert.equal(projected[1], raw[1])
  assert.equal(projected[2], raw[3])
})

test('WHAT[PROVIDER-PROJECTION-004] Y_prefix_stable_identity_deletion_preserves_surviving_raw_message_order', () => {
  const message = (id, role, parts) => ({ info: { id, role }, parts })
  const raw = [
    message('todo-call-msg', 'assistant', [
      { type: 'tool-call', tool: 'todowrite', callID: 'todo-call-1', args: { planComplete: false } },
    ]),
    message('request-local', 'assistant', [{ type: 'text', text: 'request-local presentation only' }]),
    message('todo-result-msg', 'tool', [
      { type: 'tool-result', callID: 'todo-call-1', result: { ok: true } },
    ]),
    message('covered-ordinary', 'assistant', [{ type: 'text', text: 'replace me' }]),
    message('live-u', 'user', [{ type: 'text', text: 'live request' }]),
  ]

  const projected = wire.prependCompanionMemoryByHostIds(
    raw,
    'y-prefix',
    'compressed canonical X',
    ['todo-call-msg', 'todo-result-msg', 'covered-ordinary'],
    '',
  )

  assert.deepEqual(
    projected.map(item => item.info.id),
    ['y-prefix', 'todo-call-msg', 'request-local', 'todo-result-msg', 'live-u'],
    'stable deletion may remove covered rows, but it must not reorder any raw row that survives',
  )
  assert.equal(projected[1], raw[0])
  assert.equal(projected[2], raw[1])
  assert.equal(projected[3], raw[2])
  assert.equal(projected[4], raw[4])
})

test('WHAT[PROVIDER-PROJECTION-004] same_session_Y_prefix_is_inserted_after_the_preserved_raw_Opening', () => {
  const message = (id, role, text) => ({
    info: { id, role },
    parts: [{ type: 'text', text }],
  })
  const raw = [
    message('opening-u', 'user', 'raw opening'),
    message('covered-a', 'assistant', 'covered work'),
    message('live-u', 'user', 'live request'),
  ]

  const projected = wire.prependCompanionMemoryByHostIds(
    raw,
    'y-prefix',
    'compressed post-opening history',
    ['covered-a'],
    'opening-u',
  )

  assert.deepEqual(
    projected.map(item => item.info.id),
    ['opening-u', 'y-prefix', 'live-u'],
    'FrozenRecordPrefix(includeOpening=false) must follow, never precede, the raw Opening it summarizes after',
  )
  assert.equal(projected[0], raw[0])
  assert.equal(projected[2], raw[2])
})

test('WHAT[PROVIDER-PROJECTION-004] MISC_projection_apply_rendered_prefix_both_shapes', () => {
  const raw = [{ info: { id: 'm1' } }]
  const synthetic = { name: 'SyntheticPrefix', activation: { syntheticMessageId: 'syn-9', memory: 'memory text', dropLeading: 0 } }
  const out = Projection.applyRenderedPrefix(raw, synthetic)
  assert.equal(out[0].info.id, 'syn-9')
  assert.equal(out[1].info.id, 'm1')
  assert.deepEqual(Projection.applyRenderedPrefix(raw, { name: 'PhysicalPrefix', activation: null }), raw)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_host_message_id', () => {
  assert.equal(wire.hostMessageId({ info: { id: 'via-info' }, id: 'top' }), 'via-info')
  assert.equal(wire.hostMessageId({ id: 'top' }), 'top')
  assert.equal(wire.hostMessageId({}), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_session_id_from_messages', () => {
  assert.equal(wire.projectionSessionIdFromMessages(null), null)
  assert.equal(wire.projectionSessionIdFromMessages({}), null)
  assert.equal(wire.projectionSessionIdFromMessages({ messages: [{ info: { sessionID: 's1' } }, { info: { sessionID: 's1' } }] }), 's1')
  assert.equal(wire.projectionSessionIdFromMessages({ messages: [{ info: { sessionID: 's1' } }, { info: { sessionID: 's2' } }] }), null)
  assert.equal(wire.projectionSessionIdFromMessages({ messages: [{ role: 'user' }] }), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_last_user_message_id', () => {
  const raw = [{ info: { id: 'a1' }, role: 'assistant', parts: [{ type: 'text', text: 'x' }] }, { info: { id: 'u1' }, role: 'user', parts: [{ type: 'text', text: 'q' }] }, { info: { id: 'u2' }, role: 'user', parts: [{ type: 'text', text: 'r' }] }]
  assert.equal(wire.lastUserMessageId(raw), 'u2')
  assert.equal(wire.lastUserMessageId([{ info: { id: 'a1' }, role: 'assistant', parts: [{ type: 'text', text: 'x' }] }]), null)
  assert.equal(wire.lastUserMessageId([{ role: 'user', parts: [{ type: 'text', text: 'q' }] }]), null)
})

test('WHAT[PROVIDER-PROJECTION-003] PROMPT_006_provider_attempt_uses_only_the_latest_user_turn_prompt_key', () => {
  const keyed = { info: { id: 'u-keyed', role: 'user', metadata: { wanxiangshu_prompt_key: 'prompt-old' } }, role: 'user', parts: [{ type: 'text', text: 'plugin continuation' }] }
  assert.equal(wire.lastUserPromptKey([keyed]), 'prompt-old')
  const external = { info: { id: 'u-external', role: 'user' }, role: 'user', parts: [{ type: 'text', text: 'new external root' }] }
  assert.equal(wire.lastUserPromptKey([keyed, external]), null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_formal_text_excludes_non_text_parts', () => {
  const raw = { role: 'assistant', parts: [{ type: 'text', text: 'Hello ' }, { type: 'reasoning', text: 'hidden' }, { type: 'tool-call', id: 'c1', name: 'bash' }, { type: 'text', text: 'world' }] }
  assert.equal(wire.formalText(raw), 'Hello world')
  assert.equal(wire.formalText({}), '')
  assert.equal(wire.formalText({ role: 'assistant', parts: [] }), '')
})
