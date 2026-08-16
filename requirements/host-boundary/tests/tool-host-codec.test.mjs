import assert from 'node:assert/strict'
import test from 'node:test'
import { toolCodec } from './support/host-surface.mjs'

const decode = (input) => {
  const result = toolCodec.decodeContext(input)
  return {
    sessionId: result.sessionID,
    agent: input.agent,
    toolCallId: result.callID,
    providerRunId: result.messageID,
  }
}

test('WHAT[HOST-BOUNDARY-009] HOST_011_call_id_and_message_id_present_decode_to_some', () => {
  assert.deepEqual(decode({ sessionID: 'ses_tool_1', agent: 'fast-reviewer', callID: 'call_abc', messageID: 'msg_asst_run' }), {
    sessionId: 'ses_tool_1', agent: 'fast-reviewer', toolCallId: 'call_abc', providerRunId: 'msg_asst_run',
  })
})

test('WHAT[HOST-BOUNDARY-009] HOST_011_missing_call_id_is_none_fail_closed', () => {
  const ctx = decode({ sessionID: 'ses_tool_2', messageID: 'msg_asst_run' })
  assert.equal(ctx.toolCallId, undefined)
  assert.equal(ctx.providerRunId, undefined)
})

test('WHAT[HOST-BOUNDARY-009] HOST_011_missing_message_id_is_none_fail_closed', () => {
  const ctx = decode({ sessionID: 'ses_tool_3', callID: 'call_abc' })
  assert.equal(ctx.providerRunId, undefined)
  assert.equal(ctx.toolCallId, undefined)
})

test('WHAT[HOST-BOUNDARY-009] HOST_011_both_missing_are_none', () => {
  const ctx = decode({ sessionID: 'ses_tool_4' })
  assert.equal(ctx.toolCallId, undefined)
  assert.equal(ctx.providerRunId, undefined)
})

test('WHAT[HOST-BOUNDARY-009] HOST_011_no_user_message_id_field_invented', () => {
  const ctx = decode({ sessionID: 'ses_tool_5', callID: 'call_x', messageID: 'msg_y', userMessageID: 'msg_user_should_be_ignored' })
  assert.equal(Object.prototype.hasOwnProperty.call(ctx, 'userMessageId'), false)
  assert.equal(ctx.userMessageId, undefined)
})
