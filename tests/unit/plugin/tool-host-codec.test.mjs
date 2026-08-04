// tests/unit/Plugin/tool-host-codec.test.mjs — HOST-011.
//
// ToolContext carries both halves of ReviewAttemptIdentity:
//   ProviderRunIdentity := messageID   missing → None fail-closed
//   ToolCallId          := callID      missing → None fail-closed
// userMessageID does not exist and must not be invented.

import assert from 'node:assert/strict'
import test from 'node:test'
import { toolHostCodec } from '../support/domain.mjs'

test('HOST_011_call_id_and_message_id_present_decode_to_some', () => {
  const ctx = toolHostCodec.decodeContext({
    sessionID: 'ses_tool_1',
    agent: 'fast-reviewer',
    callID: 'call_abc',
    messageID: 'msg_asst_run',
  })
  assert.deepEqual(
    {
      sessionId: ctx.sessionId,
      agent: ctx.agent,
      toolCallId: ctx.toolCallId,
      providerRunId: ctx.providerRunId,
    },
    {
      sessionId: 'ses_tool_1',
      agent: 'fast-reviewer',
      toolCallId: 'call_abc',
      providerRunId: 'msg_asst_run',
    },
  )
})

test('HOST_011_missing_call_id_is_none_fail_closed', () => {
  const ctx = toolHostCodec.decodeContext({
    sessionID: 'ses_tool_2',
    messageID: 'msg_asst_run',
  })
  assert.equal(ctx.toolCallId, undefined, 'callID missing → ToolCallId = None')
  assert.equal(ctx.providerRunId, 'msg_asst_run')
})

test('HOST_011_missing_message_id_is_none_fail_closed', () => {
  const ctx = toolHostCodec.decodeContext({
    sessionID: 'ses_tool_3',
    callID: 'call_abc',
  })
  assert.equal(ctx.providerRunId, undefined, 'messageID missing → ProviderRunId = None')
  assert.equal(ctx.toolCallId, 'call_abc')
})

test('HOST_011_both_missing_are_none', () => {
  const ctx = toolHostCodec.decodeContext({ sessionID: 'ses_tool_4' })
  assert.equal(ctx.toolCallId, undefined)
  assert.equal(ctx.providerRunId, undefined)
})

test('HOST_011_no_user_message_id_field_invented', () => {
  // HOST-011: ToolContext never carries a user message id. Decoding a raw object
  // that falsely includes one must not surface it on the typed context.
  const ctx = toolHostCodec.decodeContext({
    sessionID: 'ses_tool_5',
    callID: 'call_x',
    messageID: 'msg_y',
    userMessageID: 'msg_user_should_be_ignored',
  })
  assert.equal(Object.prototype.hasOwnProperty.call(ctx, 'userMessageId'), false)
  assert.equal(ctx.userMessageId, undefined)
  assert.deepEqual(
    { toolCallId: ctx.toolCallId, providerRunId: ctx.providerRunId },
    { toolCallId: 'call_x', providerRunId: 'msg_y' },
  )
})
