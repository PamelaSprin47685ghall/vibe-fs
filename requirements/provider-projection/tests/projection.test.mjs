// Codec/Projection: host message/request wire projections — decodePart
// branches (tool state completed/error, tool-* types, media), decodeRequest,
// prefix application, session id extraction, lastUserMessageId, formalText.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, payloadOf, listItems, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  decodePart,
  messagesFromTransformOutput,
  hostMessageId,
  projectionSessionIdFromMessages,
} = await import('../../../dist/OpenCode/Codec/ProviderWireDecode.js')
const {
  decodeMessage,
  decodeMessageView,
  decodeRequest,
  lastUserMessageId,
  formalText,
} = await import('../../../dist/OpenCode/Codec/ProviderWireCapture.js')
const {
  prependCompanionMemory,
  applyRenderedPrefix,
} = await import('../../../dist/OpenCode/Codec/ProjectionMessageEdit.js')

const { RenderedPrefix } = await import('../../../dist/Participant/Provider/Projection/Renderer.js')
const { PrefixActivation } = await import('../../../dist/Participant/Provider/Projection/Intent.js')

test('MISC_projection_decode_part_text_reasoning', () => {
  assert.equal(decodePart(null), undefined)
  const text = decodePart({ type: 'text', text: 'hi' })
  assert.equal(caseOf(text), 'WireText')
  assert.equal(payloadOf(text), 'hi')
  assert.equal(decodePart({ type: 'text' }), undefined, 'text without text field drops')
  const viaReasoning = decodePart({ type: 'reasoning', reasoning: 'r' })
  assert.equal(caseOf(viaReasoning), 'WireReasoning')
  const viaText = decodePart({ type: 'thinking', text: 't' })
  assert.equal(caseOf(viaText), 'WireReasoning')
  assert.equal(payloadOf(viaText), 't')
})

test('MISC_projection_decode_part_tool_call_states', () => {
  const withState = decodePart({ type: 'tool-call', callID: 'c1', name: 'bash', state: { status: 'completed', output: { ok: 1 } } })
  assert.equal(caseOf(withState), 'WireToolResult')
  assert.equal(payloadOf(withState)[1], '{"ok":1}')

  const errorState = decodePart({ type: 'tool-call', callId: 'c2', tool: 'bash', state: { status: 'error', errorText: 'bad' } })
  assert.equal(caseOf(errorState), 'WireToolResult')
  assert.equal(payloadOf(errorState)[1], 'bad')

  const pendingState = decodePart({ type: 'tool-call', callID: 'c3', name: 'bash', state: { status: 'running' }, arguments: { x: 1 } })
  assert.equal(caseOf(pendingState), 'WireToolCall')
  assert.equal(payloadOf(pendingState)[0].fields[0], 'c3')
  assert.deepEqual(payloadOf(pendingState).slice(1), ['bash', '{"x":1}'])

  const noState = decodePart({ type: 'tool', id: 'c4', name: 'read', args: 'raw' })
  assert.equal(caseOf(noState), 'WireToolCall')
  assert.equal(payloadOf(noState)[2], 'raw')

  const missingName = decodePart({ type: 'tool-call', id: 'c5' })
  assert.equal(missingName, undefined, 'tool-call without name is dropped')
})

test('MISC_projection_decode_part_tool_result_and_tool_prefix', () => {
  const r = decodePart({ type: 'tool_result', callID: 'c1', result: { ok: true } })
  assert.equal(caseOf(r), 'WireToolResult')
  assert.equal(payloadOf(r)[1], '{"ok":true}')

  const viaOutput = decodePart({ type: 'tool-result', id: 'c2', output: 'out' })
  assert.equal(payloadOf(viaOutput)[1], 'out')

  const noId = decodePart({ type: 'tool_result', result: 'x' })
  assert.equal(noId, undefined)

  // Unknown tool-* kinds decode as tool results (server-tool output shapes).
  const toolOutput = decodePart({ type: 'tool-output', toolCallId: 't1', output: 'done' })
  assert.equal(caseOf(toolOutput), 'WireToolResult')
  assert.equal(payloadOf(toolOutput)[0].fields[0], 't1')
  assert.equal(payloadOf(toolOutput)[1], 'done')

  const toolOutputErr = decodePart({ type: 'tool-error', callID: 't2', errorText: 'nope' })
  assert.equal(payloadOf(toolOutputErr)[1], 'nope')

  const toolNoId = decodePart({ type: 'tool-output', output: 'x' })
  assert.equal(toolNoId, undefined)
})

test('MISC_projection_decode_part_file_media', () => {
  const media = decodePart({ type: 'file', url: 'https://x/y.png', mime: 'image/png' })
  assert.equal(caseOf(media), 'WireMedia')
  assert.equal(payloadOf(media)[0], 'image/png')
  assert.match(payloadOf(media)[1], /^[0-9a-f]{64}$/, 'url digests to sha256')

  const noUrl = decodePart({ type: 'file' })
  assert.equal(noUrl, undefined)
  assert.equal(decodePart({ type: 'mystery' }), undefined)
})

test('MISC_projection_decode_message_and_request', () => {
  assert.equal(decodeMessage(null), undefined)
  const msg = decodeMessage({ role: 'user', parts: [{ type: 'text', text: 'a' }, { type: 'patch' }, { type: 'text', text: 'b' }] })
  assert.equal(msg.Role, 'user')
  assert.equal(listItems(msg.Parts).length, 2, 'bookkeeping parts are excluded')

  const viaInfo = decodeMessage({ info: { role: 'assistant' }, parts: [{ type: 'text', text: 'x' }] })
  assert.equal(viaInfo.Role, 'assistant')

  assert.equal(decodeMessage({ role: ' ', parts: [] }), undefined, 'empty message drops')

  const req = decodeRequest({
    model: { providerID: 'p', modelID: 'm', variant: 'v' },
    tools: [{ function: { name: 'fn' } }, { name: 'plain' }],
    system: ['sys1', null, 'sys2'],
    messages: [{ role: 'user', parts: [{ type: 'text', text: 'hi' }] }],
  })
  assert.equal(req.ProviderId, 'p')
  assert.equal(req.ModelId, 'm')
  assert.equal(req.Variant, 'v')
  assert.deepEqual(listItems(req.Tools), ['fn', 'plain'])
  assert.deepEqual(listItems(req.System), ['sys1', 'sys2'], 'null system entries filtered')
  assert.equal(listItems(req.Messages).length, 1)
})

test('MISC_projection_decode_request_falls_back_to_camel_and_id', () => {
  const req = decodeRequest({ model: { providerId: 'p2', modelId: 'm2' }, tools: [{ name: 't' }], system: [], messages: [] })
  assert.equal(req.ProviderId, 'p2')
  assert.equal(req.ModelId, 'm2')
  assert.equal(req.Variant, undefined)
})

test('MISC_projection_message_view_and_transform_output', () => {
  const view = decodeMessageView(toList([{ role: 'user', parts: [{ type: 'text', text: 'q' }] }]))
  assert.equal(view.ProviderId, undefined)
  assert.deepEqual(listItems(view.Tools), [])
  assert.equal(listItems(view.Messages).length, 1)

  const output = { messages: [{ role: 'user' }] }
  assert.equal(listItems(messagesFromTransformOutput(output)).length, 1)
})

test('MISC_projection_prepend_companion_memory', () => {
  const raw = [{ info: { id: 'm1' }, role: 'user' }, { info: { id: 'm2' }, role: 'user' }]
  const prefixed = listItems(prependCompanionMemory(toList(raw), 'syn-1', 'remember this', 1))
  assert.equal(prefixed.length, 2)
  assert.equal(prefixed[0].info.id, 'syn-1')
  assert.equal(prefixed[0].parts[0].text, 'remember this')
  assert.equal(prefixed[1].info.id, 'm2', 'dropLeading skips the first message')

  assert.throws(() => prependCompanionMemory(toList(raw), 's', 'm', 5), /cutoff exceeds/, 'cutoff beyond snapshot throws')
})

test('MISC_projection_apply_rendered_prefix_both_shapes', () => {
  const raw = toList([{ info: { id: 'm1' } }])
  const synthetic = new RenderedPrefix(1, [new PrefixActivation('syn-9', 'memory text', 0)])
  const out = listItems(applyRenderedPrefix(raw, synthetic))
  assert.equal(out[0].info.id, 'syn-9')
  assert.equal(out[1].info.id, 'm1')

  const physical = applyRenderedPrefix(raw, RenderedPrefix.PhysicalPrefix)
  assert.equal(physical, raw, 'physical prefix returns the list untouched')
})

test('MISC_projection_host_message_id', () => {
  assert.equal(hostMessageId({ info: { id: 'via-info' }, id: 'top' }), 'via-info')
  assert.equal(hostMessageId({ id: 'top' }), 'top')
  assert.equal(hostMessageId({}), undefined)
})

test('MISC_projection_session_id_from_messages', () => {
  assert.equal(projectionSessionIdFromMessages(null), undefined)
  assert.equal(projectionSessionIdFromMessages({}), undefined)
  assert.equal(projectionSessionIdFromMessages({ messages: [{ info: { sessionID: 's1' } }, { info: { sessionID: 's1' } }] }), 's1')
  assert.equal(projectionSessionIdFromMessages({ messages: [{ info: { sessionID: 's1' } }, { info: { sessionID: 's2' } }] }), undefined, 'multiple distinct ids are ambiguous')
  assert.equal(projectionSessionIdFromMessages({ messages: [{ role: 'user' }] }), undefined)
})

test('MISC_projection_last_user_message_id', () => {
  const raw = [
    { info: { id: 'a1' }, role: 'assistant', parts: [{ type: 'text', text: 'x' }] },
    { info: { id: 'u1' }, role: 'user', parts: [{ type: 'text', text: 'q' }] },
    { info: { id: 'u2' }, role: 'user', parts: [{ type: 'text', text: 'r' }] },
  ]
  const last = lastUserMessageId(toList(raw))
  assert.equal(last.fields[0], 'u2')

  assert.equal(lastUserMessageId(toList([{ info: { id: 'a1' }, role: 'assistant', parts: [{ type: 'text', text: 'x' }] }])), undefined)
  const noId = lastUserMessageId(toList([{ role: 'user', parts: [{ type: 'text', text: 'q' }] }]))
  assert.equal(noId, undefined, 'user message without host id contributes nothing')
})

test('MISC_projection_formal_text_excludes_non_text_parts', () => {
  const raw = {
    role: 'assistant',
    parts: [
      { type: 'text', text: 'Hello ' },
      { type: 'reasoning', text: 'hidden' },
      { type: 'tool-call', id: 'c1', name: 'bash' },
      { type: 'text', text: 'world' },
    ],
  }
  assert.equal(formalText(raw), 'Hello world')
  assert.equal(formalText({}), '')
  assert.equal(formalText({ role: 'assistant', parts: [] }), '')
})
