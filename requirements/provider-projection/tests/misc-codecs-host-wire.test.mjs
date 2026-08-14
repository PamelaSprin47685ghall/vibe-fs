// Split from tests/unit/codec/misc-codecs.test.mjs (cutover Wave 2a); owner: provider-projection.
// Host wire codec cluster (Semantic/Wire plane): OpencodeTypes records,
// HostMessageCodec decode branches, PromptIngressCodec decode. Pure
// encode/decode + malformed input — no ports, no journal.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, payloadOf } from '../../verification-system/tests/support/domain.mjs'

const {
  OpencodeModel,
  OpencodeTextPart,
  OpencodeToolCallPart,
  OpencodeCompactionPart,
  OpencodeUserMessage,
  OpencodeAssistantMessage,
  OpencodeHookInput,
  OpencodeToolExecuteInput,
  OpencodeToolExecuteOutput,
} = await import('../../../dist/OpenCode/Codec/OpencodeTypes.js')

const {
  HostMessageCodec_decodePart: decodePart,
  HostMessageCodec_decodeParts: decodeParts,
} = await import('../../../dist/OpenCode/Codec/HostMessageCodec.js')

const {
  decode: ingressDecode,
} = await import('../../../dist/Interaction/Dispatch/OpenCode/IngressCodec.js')

// ── OpencodeTypes records ────────────────────────────────────────────────────

test('MISC_opencode_types_records_carry_fields', () => {
  const model = new OpencodeModel('anthropic', 'claude', 'fast')
  assert.equal(model.providerID, 'anthropic')
  assert.equal(model.modelID, 'claude')
  assert.equal(model.variant, 'fast')

  const text = new OpencodeTextPart('p1', 'text', 'hello', true)
  assert.equal(text.id, 'p1')
  assert.equal(text.type, 'text')
  assert.equal(text.text, 'hello')
  assert.equal(text.synthetic, true)

  const call = new OpencodeToolCallPart('p2', 'tool-call', 'c1', 'read_file', { path: '/x' })
  assert.equal(call.callID, 'c1')
  assert.equal(call.tool, 'read_file')
  assert.deepEqual(call.args, { path: '/x' })

  const compact = new OpencodeCompactionPart('p3', 'compaction', true, false)
  assert.equal(compact.auto, true)
  assert.equal(compact.overflow, false)
})

test('MISC_opencode_messages_and_hook_inputs', () => {
  const user = new OpencodeUserMessage('u1', 'user', 'ses-1', 'coder', null, [])
  assert.equal(user.id, 'u1')
  assert.equal(user.role, 'user')
  assert.equal(user.sessionID, 'ses-1')
  assert.equal(user.agent, 'coder')
  assert.equal(user.model, null)
  assert.deepEqual(user.parts, [])

  const assistant = new OpencodeAssistantMessage('a1', null, 'assistant', 'ses-1', 'coder', 'anthropic', 'claude', true, { code: 'E' }, [])
  assert.equal(assistant.parentID, null)
  assert.equal(assistant.summary, true)
  assert.deepEqual(assistant.error, { code: 'E' })

  const hook = new OpencodeHookInput('ses-1', 'm1', 'coder', new OpencodeModel('p', 'm', null))
  assert.equal(hook.sessionID, 'ses-1')
  assert.equal(hook.messageID, 'm1')
  assert.equal(hook.model.providerID, 'p')

  const exec = new OpencodeToolExecuteInput('bash', 'ses-1', 'c9')
  assert.equal(exec.tool, 'bash')
  assert.equal(exec.callID, 'c9')

  const out = new OpencodeToolExecuteOutput({ cmd: 'ls' })
  assert.deepEqual(out.args, { cmd: 'ls' })
})

// ── HostMessageCodec decodePart ──────────────────────────────────────────────

test('MISC_host_message_text_and_null', () => {
  assert.equal(decodePart(null), undefined)
  const text = decodePart({ type: 'text', text: 'hi' })
  assert.equal(caseOf(text), 'Text')
  assert.equal(payloadOf(text), 'hi')
  assert.equal(decodePart({ type: 'text' }), undefined, 'text without text field drops')
  const up = decodePart({ type: 'TEXT', text: 'up' })
  assert.equal(caseOf(up), 'Text')
  assert.equal(payloadOf(up), 'up', 'type is case-insensitive')
})

test('MISC_host_message_reasoning_aliases', () => {
  const viaText = decodePart({ type: 'reasoning', text: 'think' })
  assert.equal(caseOf(viaText), 'Reasoning')
  assert.equal(payloadOf(viaText), 'think')
  const viaReasoning = decodePart({ type: 'thinking', reasoning: 'r' })
  assert.equal(payloadOf(viaReasoning), 'r')
  const viaThinking = decodePart({ type: 'reasoning', thinking: 't' })
  assert.equal(payloadOf(viaThinking), 't')
  assert.equal(decodePart({ type: 'reasoning' }), undefined)
})

test('MISC_host_message_tool_call_variants', () => {
  const snake = decodePart({ type: 'tool_call', callID: 'c1', tool: 'bash', args: { cmd: 'ls' } })
  assert.equal(caseOf(snake), 'ToolCall')
  assert.deepEqual(payloadOf(snake), ['c1', 'bash', '{"cmd":"ls"}'])

  // camelCase + arguments alias + id fallback
  const camel = decodePart({ type: 'tool-call', callId: 'c2', name: 'read', arguments: { p: 1 } })
  assert.deepEqual(payloadOf(camel), ['c2', 'read', '{"p":1}'])

  const idFallback = decodePart({ type: 'tool', id: 'c3', name: 'x' })
  assert.deepEqual(payloadOf(idFallback), ['c3', 'x', '{}'])

  // string args pass through verbatim; missing args default {}
  const strArgs = decodePart({ type: 'tool-call', id: 'c4', tool: 'x', args: 'raw' })
  assert.deepEqual(payloadOf(strArgs), ['c4', 'x', 'raw'])
  const nullArgs = decodePart({ type: 'tool-call', id: 'c5', tool: 'x', args: null })
  assert.deepEqual(payloadOf(nullArgs), ['c5', 'x', '{}'], 'null args fall back to the empty canonical form')

  // anonymous tool-call with no name and no id is dropped
  assert.equal(decodePart({ type: 'tool-call', args: {} }), undefined)
})

test('MISC_host_message_session_tool_state_controls_call_vs_result', () => {
  const pending = decodePart({
    type: 'tool',
    id: 'part-pending',
    callID: 'c-pending',
    tool: 'read',
    state: { status: 'pending', input: { filePath: 'a.txt' } },
  })
  assert.equal(caseOf(pending), 'ToolCall')
  assert.deepEqual(payloadOf(pending), ['c-pending', 'read', '{"filePath":"a.txt"}'])

  const running = decodePart({
    type: 'tool',
    id: 'part-running',
    callID: 'c-running',
    tool: 'grep',
    state: { status: 'running', input: { pattern: 'needle' } },
  })
  assert.equal(caseOf(running), 'ToolCall')
  assert.deepEqual(payloadOf(running), ['c-running', 'grep', '{"pattern":"needle"}'])

  const completed = decodePart({
    type: 'tool',
    id: 'part-completed',
    callID: 'c-completed',
    tool: 'read',
    state: { status: 'completed', input: { filePath: 'a.txt' }, output: 'done' },
  })
  assert.equal(caseOf(completed), 'ToolResult')
  assert.deepEqual(payloadOf(completed), ['c-completed', 'done'])

  const failed = decodePart({
    type: 'tool',
    id: 'part-error',
    callID: 'c-error',
    tool: 'read',
    state: { status: 'error', input: { filePath: 'a.txt' }, error: 'native failure' },
  })
  assert.equal(caseOf(failed), 'ToolResult')
  assert.deepEqual(payloadOf(failed), ['c-error', 'native failure'])
})

test('MISC_host_message_tool_result_variants', () => {
  const r = decodePart({ type: 'tool_result', callID: 'c1', result: { ok: true } })
  assert.equal(caseOf(r), 'ToolResult')
  assert.deepEqual(payloadOf(r), ['c1', '{"ok":true}'])

  const viaOutput = decodePart({ type: 'tool-result', callId: 'c2', output: 'out' })
  assert.deepEqual(payloadOf(viaOutput), ['c2', 'out'])

  const viaContent = decodePart({ type: 'tool-result', id: 'c3', content: { n: 2 } })
  assert.deepEqual(payloadOf(viaContent), ['c3', '{"n":2}'])

  const empty = decodePart({ type: 'tool-result', id: 'c4' })
  assert.deepEqual(payloadOf(empty), ['c4', 'null'])
})

test('MISC_host_message_activity_kinds_normalize_underscores', () => {
  assert.equal(payloadOf(decodePart({ type: 'patch' })), 'patch')
  assert.equal(payloadOf(decodePart({ type: 'step-start' })), 'step-start')
  assert.equal(payloadOf(decodePart({ type: 'step_finish' })), 'step-finish')
  assert.equal(payloadOf(decodePart({ type: 'step_start' })), 'step-start')
  assert.equal(decodePart({ type: 'nonsense' }), undefined)
  assert.equal(decodePart({ type: '' }), undefined)
})

test('MISC_host_message_decode_parts_filters_and_preserves_order', () => {
  assert.deepEqual(decodeParts(null), [])
  assert.deepEqual(decodeParts([]), [])
  const parts = decodeParts([
    { type: 'text', text: 'a' },
    { type: 'bogus' },
    { type: 'tool-call', id: 't1', tool: 'bash', args: {} },
    null,
    { type: 'text' },
  ])
  assert.equal(parts.length, 2)
  assert.equal(caseOf(parts[0]), 'Text')
  assert.equal(caseOf(parts[1]), 'ToolCall')
  assert.equal(payloadOf(parts[1])[0], 't1')
})

// ── PromptIngressCodec decode ────────────────────────────────────────────────

test('MISC_ingress_session_id_sources', () => {
  const viaSession = ingressDecode({ session: 's1' }, {})
  assert.equal(viaSession.SessionId.fields[0], 's1')
  const viaSessionID = ingressDecode({ sessionID: 's2' }, {})
  assert.equal(viaSessionID.SessionId.fields[0], 's2')
  const viaSessionId = ingressDecode({ sessionId: 's3' }, {})
  assert.equal(viaSessionId.SessionId.fields[0], 's3')
  const none = ingressDecode({}, {})
  assert.equal(none.SessionId, undefined)
})

test('MISC_ingress_message_id_sources', () => {
  const fromInput = ingressDecode({ messageID: 'm1' }, {})
  assert.equal(fromInput.PhysicalUserMessageId.fields[0], 'm1')
  const fromInputCamel = ingressDecode({ messageId: 'm2' }, {})
  assert.equal(fromInputCamel.PhysicalUserMessageId.fields[0], 'm2')
  const fromOutput = ingressDecode({}, { id: 'm3' })
  assert.equal(fromOutput.PhysicalUserMessageId.fields[0], 'm3')
  const fromOutputMessage = ingressDecode({}, { message: { id: 'm4' } })
  assert.equal(fromOutputMessage.PhysicalUserMessageId.fields[0], 'm4')
  const fromOutputInfo = ingressDecode({}, { info: { id: 'm5' } })
  assert.equal(fromOutputInfo.PhysicalUserMessageId.fields[0], 'm5')
  const none = ingressDecode({}, {})
  assert.equal(none.PhysicalUserMessageId, undefined)
})

test('MISC_ingress_agent_sources', () => {
  assert.equal(ingressDecode({ agent: 'coder' }, {}).ExplicitAgent, 'coder')
  assert.equal(ingressDecode({ message: { agent: 'reviewer' } }, {}).ExplicitAgent, 'reviewer')
  assert.equal(ingressDecode({}, { agent: 'planner' }).ExplicitAgent, 'planner')
  assert.equal(ingressDecode({}, {}).ExplicitAgent, undefined)
})

test('MISC_ingress_prompt_key_from_metadata', () => {
  const fromInput = ingressDecode({ metadata: { wanxiangshu_prompt_key: 'pk-1' } }, {})
  assert.equal(fromInput.PromptKey.fields[0], 'pk-1')
  const blankInput = ingressDecode({ metadata: { wanxiangshu_prompt_key: '   ' } }, {})
  assert.equal(blankInput.PromptKey, undefined)
  const fromOutputPart = ingressDecode({}, { parts: [{ metadata: { wanxiangshu_prompt_key: 'pk-2' } }] })
  assert.equal(fromOutputPart.PromptKey.fields[0], 'pk-2')
  const none = ingressDecode({}, { parts: [{}] })
  assert.equal(none.PromptKey, undefined)
})

test('MISC_ingress_host_compaction_detection', () => {
  assert.equal(ingressDecode({}, { parts: [{ type: 'compaction' }] }).IsHostCompaction, true)
  assert.equal(ingressDecode({}, { message: { summary: true } }).IsHostCompaction, true)
  assert.equal(ingressDecode({}, { message: { agent: 'compaction' } }).IsHostCompaction, true)
  assert.equal(ingressDecode({}, { message: { mode: 'compaction' } }).IsHostCompaction, true)
  assert.equal(ingressDecode({}, { message: { mode: 'chat' } }).IsHostCompaction, false)
  assert.equal(ingressDecode({}, {}).IsHostCompaction, false)
  assert.equal(ingressDecode({}, null).IsHostCompaction, false)
})

test('MISC_ingress_text_joins_text_parts_and_filters_blanks', () => {
  const msg = ingressDecode({}, { parts: [{ type: 'text', text: 'one' }, { type: 'text', text: '   ' }, { type: 'tool-call', tool: 'x' }, { type: 'text', text: 'two' }] })
  assert.equal(msg.Text, 'one\ntwo')
  assert.equal(ingressDecode({}, { parts: [{ type: 'text' }] }).Text, undefined)
  assert.equal(ingressDecode({}, {}).Text, undefined)
})
