import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const wire = await import('../../../dist/OpenCode/Codec/ProviderProjectionSurface.js')
const {
  opencodeModel,
  opencodeTextPart,
  opencodeToolCallPart,
  opencodeCompactionPart,
  opencodeUserMessage,
  opencodeAssistantMessage,
  opencodeHookInput,
  opencodeToolExecuteInput,
  opencodeToolExecuteOutput,
  decodeHostPart,
  decodeHostParts,
  decodeIngress,
} = wire

test('WHAT[PROVIDER-PROJECTION-003] MISC_opencode_types_records_carry_fields', () => {
  const model = opencodeModel('anthropic', 'claude', 'fast')
  assert.equal(model.providerID, 'anthropic')
  assert.equal(model.modelID, 'claude')
  assert.equal(model.variant, 'fast')

  const text = opencodeTextPart('p1', 'text', 'hello', true)
  assert.equal(text.id, 'p1')
  assert.equal(text.type, 'text')
  assert.equal(text.text, 'hello')
  assert.equal(text.synthetic, true)

  const call = opencodeToolCallPart('p2', 'tool-call', 'c1', 'read_file', { path: '/x' })
  assert.equal(call.callID, 'c1')
  assert.equal(call.tool, 'read_file')
  assert.deepEqual(call.args, { path: '/x' })

  const compact = opencodeCompactionPart('p3', 'compaction', true, false)
  assert.equal(compact.auto, true)
  assert.equal(compact.overflow, false)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_opencode_messages_and_hook_inputs', () => {
  const user = opencodeUserMessage('u1', 'user', 'ses-1', 'coder', null, [])
  assert.equal(user.id, 'u1')
  assert.equal(user.role, 'user')
  assert.equal(user.sessionID, 'ses-1')
  assert.equal(user.agent, 'coder')
  assert.equal(user.model, null)
  assert.deepEqual(user.parts, [])

  const assistant = opencodeAssistantMessage('a1', null, 'assistant', 'ses-1', 'coder', 'anthropic', 'claude', true, { code: 'E' }, [])
  assert.equal(assistant.parentID, null)
  assert.equal(assistant.summary, true)
  assert.deepEqual(assistant.error, { code: 'E' })

  const hook = opencodeHookInput('ses-1', 'm1', 'coder', opencodeModel('p', 'm', null))
  assert.equal(hook.sessionID, 'ses-1')
  assert.equal(hook.messageID, 'm1')
  assert.equal(hook.model.providerID, 'p')

  const exec = opencodeToolExecuteInput('bash', 'ses-1', 'c9')
  assert.equal(exec.tool, 'bash')
  assert.equal(exec.callID, 'c9')

  const out = opencodeToolExecuteOutput({ cmd: 'ls' })
  assert.deepEqual(out.args, { cmd: 'ls' })
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_text_and_null', () => {
  assert.equal(decodeHostPart(null), null)
  assert.deepEqual(decodeHostPart({ type: 'text', text: 'hi' }), { kind: 'Text', text: 'hi' })
  assert.equal(decodeHostPart({ type: 'text' }), null)
  assert.deepEqual(decodeHostPart({ type: 'TEXT', text: 'up' }), { kind: 'Text', text: 'up' })
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_reasoning_aliases', () => {
  assert.deepEqual(decodeHostPart({ type: 'reasoning', text: 'think' }), { kind: 'Reasoning', text: 'think' })
  assert.deepEqual(decodeHostPart({ type: 'thinking', reasoning: 'r' }), { kind: 'Reasoning', text: 'r' })
  assert.deepEqual(decodeHostPart({ type: 'reasoning', thinking: 't' }), { kind: 'Reasoning', text: 't' })
  assert.equal(decodeHostPart({ type: 'reasoning' }), null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_tool_call_variants', () => {
  assert.deepEqual(decodeHostPart({ type: 'tool_call', callID: 'c1', tool: 'bash', args: { cmd: 'ls' } }), {
    kind: 'ToolCall', callId: 'c1', name: 'bash', args: '{"cmd":"ls"}',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool-call', callId: 'c2', name: 'read', arguments: { p: 1 } }), {
    kind: 'ToolCall', callId: 'c2', name: 'read', args: '{"p":1}',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool', id: 'c3', name: 'x' }), {
    kind: 'ToolCall', callId: 'c3', name: 'x', args: '{}',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool-call', id: 'c4', tool: 'x', args: 'raw' }), {
    kind: 'ToolCall', callId: 'c4', name: 'x', args: 'raw',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool-call', id: 'c5', tool: 'x', args: null }), {
    kind: 'ToolCall', callId: 'c5', name: 'x', args: '{}',
  })
  assert.equal(decodeHostPart({ type: 'tool-call', args: {} }), null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_session_tool_state_controls_call_vs_result', () => {
  assert.deepEqual(decodeHostPart({ type: 'tool', id: 'part-pending', callID: 'c-pending', tool: 'read', state: { status: 'pending', input: { filePath: 'a.txt' } } }), {
    kind: 'ToolCall', callId: 'c-pending', name: 'read', args: '{"filePath":"a.txt"}',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool', id: 'part-running', callID: 'c-running', tool: 'grep', state: { status: 'running', input: { pattern: 'needle' } } }), {
    kind: 'ToolCall', callId: 'c-running', name: 'grep', args: '{"pattern":"needle"}',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool', id: 'part-completed', callID: 'c-completed', tool: 'read', state: { status: 'completed', output: 'done' } }), {
    kind: 'ToolResult', callId: 'c-completed', result: 'done',
  })
  assert.deepEqual(decodeHostPart({ type: 'tool', id: 'part-error', callID: 'c-error', tool: 'read', state: { status: 'error', error: 'native failure' } }), {
    kind: 'ToolResult', callId: 'c-error', result: 'native failure',
  })
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_tool_result_variants', () => {
  assert.deepEqual(decodeHostPart({ type: 'tool_result', callID: 'c1', result: { ok: true } }), { kind: 'ToolResult', callId: 'c1', result: '{"ok":true}' })
  assert.deepEqual(decodeHostPart({ type: 'tool-result', callId: 'c2', output: 'out' }), { kind: 'ToolResult', callId: 'c2', result: 'out' })
  assert.deepEqual(decodeHostPart({ type: 'tool-result', id: 'c3', content: { n: 2 } }), { kind: 'ToolResult', callId: 'c3', result: '{"n":2}' })
  assert.deepEqual(decodeHostPart({ type: 'tool-result', id: 'c4' }), { kind: 'ToolResult', callId: 'c4', result: 'null' })
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_activity_kinds_normalize_underscores', () => {
  assert.deepEqual(decodeHostPart({ type: 'patch' }), { kind: 'Activity', activity: 'patch' })
  assert.deepEqual(decodeHostPart({ type: 'step-start' }), { kind: 'Activity', activity: 'step-start' })
  assert.deepEqual(decodeHostPart({ type: 'step_finish' }), { kind: 'Activity', activity: 'step-finish' })
  assert.deepEqual(decodeHostPart({ type: 'step_start' }), { kind: 'Activity', activity: 'step-start' })
  assert.equal(decodeHostPart({ type: 'nonsense' }), null)
  assert.equal(decodeHostPart({ type: '' }), null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_host_message_decode_parts_filters_and_preserves_order', () => {
  assert.deepEqual(decodeHostParts(null), [])
  assert.deepEqual(decodeHostParts([]), [])
  const parts = decodeHostParts([{ type: 'text', text: 'a' }, { type: 'bogus' }, { type: 'tool-call', id: 't1', tool: 'bash', args: {} }, null, { type: 'text' }])
  assert.deepEqual(parts, [{ kind: 'Text', text: 'a' }, { kind: 'ToolCall', callId: 't1', name: 'bash', args: '{}' }])
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_session_id_sources', () => {
  assert.equal(decodeIngress({ session: 's1' }, {}).sessionId, 's1')
  assert.equal(decodeIngress({ sessionID: 's2' }, {}).sessionId, 's2')
  assert.equal(decodeIngress({ sessionId: 's3' }, {}).sessionId, 's3')
  assert.equal(decodeIngress({ session: { id: 's4' } }, {}).sessionId, 's4')
  assert.equal(decodeIngress({ session: { sessionID: 's5' } }, {}).sessionId, 's5')
  assert.equal(decodeIngress({ session: { sessionId: 's6' } }, {}).sessionId, 's6')
  assert.equal(decodeIngress({ session: {} }, {}).sessionId, null)
  assert.equal(decodeIngress({ session: 7 }, {}).sessionId, null)
  assert.equal(decodeIngress({ session: { id: 7 } }, {}).sessionId, null)
  assert.equal(decodeIngress({ sessionID: 7 }, {}).sessionId, null)
  assert.equal(decodeIngress({ session: '  ' }, {}).sessionId, null)
  assert.equal(decodeIngress({ session: { id: '' } }, {}).sessionId, null)
  assert.equal(decodeIngress({}, {}).sessionId, null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_agent_sources', () => {
  assert.equal(decodeIngress({ agent: 'coder' }, {}).explicitAgent, 'coder')
  assert.equal(decodeIngress({ message: { agent: 'reviewer' } }, {}).explicitAgent, 'reviewer')
  assert.equal(decodeIngress({}, { agent: 'planner' }).explicitAgent, 'planner')
  assert.equal(decodeIngress({}, {}).explicitAgent, null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_prompt_key_from_metadata', () => {
  assert.equal(decodeIngress({ metadata: { wanxiangshu_prompt_key: 'pk-1' } }, {}).promptKey, 'pk-1')
  assert.equal(decodeIngress({ metadata: { wanxiangshu_prompt_key: '   ' } }, {}).promptKey, null)
  assert.equal(decodeIngress({}, { parts: [{ metadata: { wanxiangshu_prompt_key: 'pk-2' } }] }).promptKey, 'pk-2')
  assert.equal(decodeIngress({}, { parts: [{}] }).promptKey, null)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_host_compaction_detection', () => {
  assert.equal(decodeIngress({}, { parts: [{ type: 'compaction' }] }).isHostCompaction, true)
  assert.equal(decodeIngress({}, { message: { summary: true } }).isHostCompaction, true)
  assert.equal(decodeIngress({}, { message: { agent: 'compaction' } }).isHostCompaction, true)
  assert.equal(decodeIngress({}, { message: { mode: 'compaction' } }).isHostCompaction, true)
  assert.equal(decodeIngress({}, { message: { mode: 'chat' } }).isHostCompaction, false)
  assert.equal(decodeIngress({}, {}).isHostCompaction, false)
  assert.equal(decodeIngress({}, null).isHostCompaction, false)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_host_synthetic_detection', () => {
  assert.equal(decodeIngress({}, { parts: [{ synthetic: true }] }).isHostSynthetic, true)
  assert.equal(decodeIngress({}, { parts: [{ synthetic: false }] }).isHostSynthetic, false)
  assert.equal(decodeIngress({}, {}).isHostSynthetic, false)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_host_synthetic_requires_fully_injected_material', () => {
  // A user message that mentions a file gains host-injected synthetic read
  // echoes alongside its real parts. Only material the Host generated outright
  // is HostInternal; mixed payloads are external user material.
  assert.equal(decodeIngress({}, { parts: [{ type: 'text', text: 'a' }, { synthetic: true }] }).isHostSynthetic, false)
  assert.equal(decodeIngress({}, { parts: [{ synthetic: true }, { type: 'text', text: 'a' }] }).isHostSynthetic, false)
  assert.equal(decodeIngress({}, { parts: [{ synthetic: true }, { type: 'file' }] }).isHostSynthetic, false)
  assert.equal(decodeIngress({}, { parts: [{ synthetic: true }, {}] }).isHostSynthetic, false)
  assert.equal(decodeIngress({}, { parts: [{ synthetic: true }, { synthetic: true }] }).isHostSynthetic, true)
})
test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_text_joins_text_parts_and_filters_blanks', () => {
  const msg = decodeIngress({}, { parts: [{ type: 'text', text: 'one' }, { type: 'text', text: '   ' }, { type: 'tool-call', tool: 'x' }, { type: 'text', text: 'two' }] })
  assert.equal(msg.text, 'one\ntwo')
  assert.equal(decodeIngress({}, { parts: [{ type: 'text' }] }).text, null)
  assert.equal(decodeIngress({}, {}).text, null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");

const H = (text) => `H(${text})`
const message = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const row = (role, text, hostMessageId = null, hostIsPhysical = false) => ({
  message: message(role, text),
  hostMessageId,
  hostIsPhysical,
})
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const base = (key, rows) => Projection.replaceMessageBase({ key, rows })
const insert = (key, anchor, rows) => Projection.insertMessageRows({ key, anchor, rows })
const before = (index) => ({ kind: 'BeforeMessageIndex', index })
const append = { kind: 'Append' }

test('WHAT[PROVIDER-PROJECTION-003] rendered rows equal the decode of their Host writeback shape', () => {
  const intent = base('base', [
    row('user', 'physical', 'physical-id', true),
    row('assistant', 'synthetic', 'synthetic-id', false),
  ])
  const rendered = Projection.renderMessagesWithHostIds(snapshot(), [], [intent])
  const hostWriteback = rendered.messages.map((item, index) => ({
    info: {
      id: rendered.hostMessageIds[index],
      role: item.role,
    },
    parts: item.parts.map(part => ({ type: part.kind, text: part.text })),
  }))

  assert.deepEqual(Projection.decodeMessages(hostWriteback).messages, rendered.messages)
  assert.equal(
    Projection.renderWire(Projection.decodeMessages(hostWriteback).messages),
    Projection.renderWire(rendered.messages),
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

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
test('WHAT[PROVIDER-PROJECTION-003] MISC_projection_host_message_id', () => {
  assert.equal(wire.hostMessageId({ info: { id: 'via-info' }, id: 'top' }), 'via-info')
  assert.equal(wire.hostMessageId({ id: 'top' }), 'top')
  assert.equal(wire.hostMessageId({}), null)
})
test('WHAT[PROVIDER-PROJECTION-003] retry continuation origin stays in Host metadata, outside provider semantics', () => {
  const retry = {
    info: { id: 'retry-1', role: 'user' },
    parts: [{
      type: 'text',
      text: 'continue',
      metadata: { wanxiangshu_origin: 'ProviderRetryAttempt' },
    }],
  }
  assert.equal(wire.promptOriginOfMessage(retry), 'ProviderRetryAttempt')
  assert.equal(wire.semanticTurnOfHostMessageId('retry-1', [
    { info: { id: 'root', role: 'user' }, parts: [{ type: 'text', text: 'root' }] },
    retry,
  ]), 1)
  assert.equal(wire.decodeMessageView([retry]).messages[0].parts[0].text, 'continue')
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toolModule = await import("@opencode-ai/plugin/tool");

const codec = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')
const {
  makeArguments: makeArgs,
  argumentText,
  argumentOptionalText,
  argumentOptionalTexts,
  argumentOptionalNumber,
  schemaString,
  schemaStringDescribed,
  schemaNumber,
  schemaEnum,
  schemaEnumDescribed,
  schemaOptionalEnum,
  schemaOptionalEnumDescribed,
  schemaManagedOrHandle,
  schemaOptionalString,
  schemaOptionalStringDescribed,
  schemaOptionalNumber,
  schemaOptionalNonNegativeIntegerDescribed,
  schemaOptionalStringArray,
  registryNames,
  hide,
  contextDecode,
  contextView,
  contextAttachAbort,
  tomlObject,
  tomlObjectWithInstructions,
  tomlTable,
  looksLikeHandleId,
  digest,
} = codec

test('WHAT[PROVIDER-PROJECTION-003] CODEC_looks_like_handle_id_shape', () => {
  assert.equal(looksLikeHandleId('ab12cd'), true)
  assert.equal(looksLikeHandleId('zz9900'), true)
  assert.equal(looksLikeHandleId('ab12'), false)
  assert.equal(looksLikeHandleId('ab12cdef'), false)
  assert.equal(looksLikeHandleId('AB12CD'), false)
  assert.equal(looksLikeHandleId('ab-12c'), false)
  assert.equal(looksLikeHandleId(''), false)
  assert.equal(looksLikeHandleId('      '), false)
})
test('WHAT[PROVIDER-PROJECTION-003] CODEC_digest_is_true_fnv1a_32bit', () => {
  const reference = (text) => {
    let hash = 2166136261n
    for (const byte of new TextEncoder().encode(text)) hash = ((hash ^ BigInt(byte)) * 16777619n) & 0xffffffffn
    return 'fnv1a:' + hash.toString(16).padStart(8, '0')
  }
  for (const text of ['', 'a', 'wanxiangshu', 'the quick brown fox jumps over the lazy dog', 'fnv1a must wrap at 32 bits']) assert.equal(digest(text), reference(text))
  assert.notEqual(digest('a'), digest('b'))
  assert.match(digest('anything'), /^fnv1a:[0-9a-f]{8}$/)
})
}
