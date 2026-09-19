import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SessionSnapshotSurface = await import("../../../dist/OpenCode/Host/SessionSnapshotSurface.js");

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall
const toolPartStateAt = SessionSnapshotSurface.toolPartStateAt
const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[host-boundary-009] TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const messages = projectMessages([
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Ambiguous')
  assert.equal(located.toolCallId, 'call_todo')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { contextAttachAbort, contextDecode } = await import("../../../dist/OpenCode/Codec/ToolHostSurface.js");


test('WHAT[host-boundary-009] HOST_abort_callback_fires_once_immediately_or_from_the_registered_unit_listener', () => {
  let immediate = 0
  contextAttachAbort(contextDecode({ sessionID: 'immediate', abort: { aborted: true, addEventListener() {}, removeEventListener() {} } }), () => { immediate += 1 })
  assert.equal(immediate, 1)

  let registration
  const removed = []
  const signal = {
    aborted: false,
    addEventListener: (name, listener, options) => { registration = { name, listener, options } },
    removeEventListener: (name, listener) => removed.push({ name, listener }),
  }
  let deferred = 0
  const unsubscribe = contextAttachAbort(contextDecode({ sessionID: 'deferred', abortSignal: signal }), () => { deferred += 1 })
  assert.deepEqual({ name: registration.name, once: registration.options.once }, { name: 'abort', once: true })
  registration.listener()
  assert.equal(deferred, 1)
  unsubscribe()
  assert.deepEqual(removed, [{ name: 'abort', listener: registration.listener }])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toolHost = await import("../../../dist/OpenCode/Codec/ToolHostSurface.js");

const decode = (input) => {
  const result = toolHost.contextView(toolHost.contextDecode(input))
  return {
    sessionId: result.sessionId,
    agent: result.agent ?? undefined,
    toolCallId: result.toolCallId ?? undefined,
    providerRunId: result.providerRunId ?? undefined,
  }
}

test('WHAT[host-boundary-009] HOST_011_call_id_and_message_id_present_decode_to_some', () => {
  assert.deepEqual(decode({ sessionID: 'ses_tool_1', agent: 'reviewer', callID: 'call_abc', messageID: 'msg_asst_run' }), {
    sessionId: 'ses_tool_1', agent: 'reviewer', toolCallId: 'call_abc', providerRunId: 'msg_asst_run',
  })
})
test('WHAT[host-boundary-009] HOST_011_missing_call_id_is_none_fail_closed', () => {
  const ctx = decode({ sessionID: 'ses_tool_2', messageID: 'msg_asst_run' })
  assert.equal(ctx.toolCallId, undefined)
  assert.equal(ctx.providerRunId, undefined)
})
test('WHAT[host-boundary-009] HOST_011_missing_message_id_is_none_fail_closed', () => {
  const ctx = decode({ sessionID: 'ses_tool_3', callID: 'call_abc' })
  assert.equal(ctx.providerRunId, undefined)
  assert.equal(ctx.toolCallId, undefined)
})
test('WHAT[host-boundary-009] HOST_011_both_missing_are_none', () => {
  const ctx = decode({ sessionID: 'ses_tool_4' })
  assert.equal(ctx.toolCallId, undefined)
  assert.equal(ctx.providerRunId, undefined)
})
test('WHAT[host-boundary-009] HOST_011_no_user_message_id_field_invented', () => {
  const ctx = decode({ sessionID: 'ses_tool_5', callID: 'call_x', messageID: 'msg_y', userMessageID: 'msg_user_should_be_ignored' })
  assert.equal(Object.prototype.hasOwnProperty.call(ctx, 'userMessageId'), false)
  assert.equal(ctx.userMessageId, undefined)
})
}
