// Host wire codec cluster (semantic owner: provider projection).
import assert from 'node:assert/strict'
import test from 'node:test'

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
  assert.equal(decodeIngress({}, {}).sessionId, null)
})

test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_message_id_sources', () => {
  assert.equal(decodeIngress({ messageID: 'm1' }, {}).physicalUserMessageId, 'm1')
  assert.equal(decodeIngress({ messageId: 'm2' }, {}).physicalUserMessageId, null, 'unsupported spelling fails closed')
  assert.equal(decodeIngress({}, { id: 'm3' }).physicalUserMessageId, 'm3')
  assert.equal(decodeIngress({}, { message: { id: 'm4' } }).physicalUserMessageId, 'm4')
  assert.equal(decodeIngress({}, { info: { id: 'm5' } }).physicalUserMessageId, 'm5')
  assert.equal(decodeIngress({}, {}).physicalUserMessageId, null)
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

test('WHAT[PROVIDER-PROJECTION-003] MISC_ingress_text_joins_text_parts_and_filters_blanks', () => {
  const msg = decodeIngress({}, { parts: [{ type: 'text', text: 'one' }, { type: 'text', text: '   ' }, { type: 'tool-call', tool: 'x' }, { type: 'text', text: 'two' }] })
  assert.equal(msg.text, 'one\ntwo')
  assert.equal(decodeIngress({}, { parts: [{ type: 'text' }] }).text, null)
  assert.equal(decodeIngress({}, {}).text, null)
})
