// PPT: PairProgrammingThoughtTransform — HOST-013 permanent pair injection.

import assert from 'node:assert/strict'
import test from 'node:test'

import { toList, listItems } from '../support/domain.mjs'

const {
  tryInject,
  isPairProgrammingThought,
  source,
  text,
  stableCallId,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

const inject = (session, raw, markerText = text) => {
  const out = tryInject(undefined, session, markerText, toList(raw))
  return out === undefined ? undefined : listItems(out)
}

const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})

const assistantText = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'text', text: 'ok' }],
})

const toolCall = (id, tool, callID) => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'pending', input: {}, time: { start: 0 } },
  }],
})

const toolResult = (id, tool, callID, output = 'ok') => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'completed', input: {}, output, time: { start: 0, end: 0 } },
  }],
})

const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))

const assertPairShape = (callMsg, resultMsg, callId, markerText) => {
  assert.equal(callMsg.info.role, 'assistant')
  assert.equal(callMsg.info.source, source)
  assert.equal(callMsg.info.synthetic, true)
  assert.equal(callMsg.parts.length, 1)
  assert.equal(callMsg.parts[0].type, 'tool')
  assert.equal(callMsg.parts[0].tool, 'auto-injected')
  assert.equal(callMsg.parts[0].callID, callId)
  assert.equal(callMsg.parts[0].state.status, 'pending')

  assert.equal(resultMsg.info.role, 'assistant')
  assert.equal(resultMsg.info.source, source)
  assert.equal(resultMsg.info.synthetic, true)
  assert.equal(resultMsg.parts[0].type, 'tool')
  assert.equal(resultMsg.parts[0].tool, 'auto-injected')
  assert.equal(resultMsg.parts[0].callID, callId)
  assert.equal(resultMsg.parts[0].state.status, 'completed')
  assert.equal(resultMsg.parts[0].state.output, markerText)
}

test('PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-auto-injected')
  assert.ok(text.length > 0, 'frozen thought text must be non-empty')
  assert.equal(isPairProgrammingThought(null), false)
  assert.equal(isPairProgrammingThought({}), false)
  assert.equal(isPairProgrammingThought({ info: { source: 'other' } }), false)
  assert.equal(isPairProgrammingThought({ info: { source } }), true)
  assert.equal(isPairProgrammingThought({ parts: [] }), false, 'no info.source means not a marker')
})

test('PPT_tryInject_appends_pair_on_empty_history_without_anchor', () => {
  const out = inject('ses_empty', [])
  assert.ok(out, 'empty history must still append one pair')
  assert.equal(out.length, 2)
  const callId = stableCallId('ses_empty', 1n)
  assertPairShape(out[0], out[1], callId, text)
})

test('PPT_tryInject_places_pair_before_trailing_user', () => {
  const raw = [userMsg('msg_1')]
  const out = inject('ses_1', raw)
  assert.ok(out)
  assert.equal(out.length, 3)
  const callId = stableCallId('ses_1', 1n)
  assertPairShape(out[0], out[1], callId, text)
  assert.deepEqual(out[2], raw[0], 'trailing user stays last')
})

test('PPT_tryInject_places_pair_before_trailing_user_with_prior_assistant', () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('u2', 'steer')]
  const out = inject('ses_assistant', raw)
  assert.ok(out)
  assert.equal(out.length, 5)
  assert.deepEqual(out[0], raw[0])
  assert.deepEqual(out[1], raw[1])
  const callId = stableCallId('ses_assistant', 1n)
  assertPairShape(out[2], out[3], callId, text)
  assert.deepEqual(out[4], raw[2], 'steer user remains after pair')
})

test('PPT_tryInject_merges_into_tool_batches_before_user', () => {
  const raw = [
    toolCall('c1', 'bash', 't1'),
    toolCall('c2', 'read', 't2'),
    toolResult('r1', 'bash', 't1', 'out1'),
    toolResult('r2', 'read', 't2', 'out2'),
    userMsg('u1', 'steer'),
  ]
  const out = inject('ses_tools', raw)
  assert.ok(out)
  assert.equal(out.length, 7)
  const callId = stableCallId('ses_tools', 1n)

  assert.equal(out[0].parts[0].tool, 'bash')
  assert.equal(out[1].parts[0].tool, 'read')
  assert.equal(out[2].parts[0].tool, 'auto-injected')
  assert.equal(out[2].parts[0].state.status, 'pending')
  assert.equal(out[2].parts[0].callID, callId)
  assert.equal(out[3].parts[0].tool, 'bash')
  assert.equal(out[3].parts[0].state.status, 'completed')
  assert.equal(out[4].parts[0].tool, 'read')
  assert.equal(out[4].parts[0].state.status, 'completed')
  assert.equal(out[5].parts[0].tool, 'auto-injected')
  assert.equal(out[5].parts[0].state.status, 'completed')
  assert.equal(out[5].parts[0].callID, callId)
  assert.equal(out[5].parts[0].state.output, text)
  assert.deepEqual(out[6], raw[4])
})

test('PPT_tryInject_second_pass_keeps_history_before_trailing_user', () => {
  const once = inject('ses_append', [userMsg('msg_1')])
  assert.ok(once)
  assert.equal(once.length, 3)

  const twice = inject('ses_append', [userMsg('msg_1')])
  assert.ok(twice)
  assert.equal(twice.length, 5, 'history pair + new pair + user')
  assert.equal(pairMessages(twice).length, 4)

  const firstCall = stableCallId('ses_append', 1n)
  const secondCall = stableCallId('ses_append', 2n)
  assert.notEqual(firstCall, secondCall)
  assertPairShape(twice[0], twice[1], firstCall, text)
  assertPairShape(twice[2], twice[3], secondCall, text)
  assert.equal(twice[4].info.role, 'user')
})

test('PPT_tryInject_call_id_is_stable_per_session_and_ordinal', () => {
  assert.equal(stableCallId('ses_1', 1n), stableCallId('ses_1', 1n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_1', 2n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_2', 1n))
})

test('PPT_tryInject_without_session_id_still_appends_stable_pair', () => {
  const out = inject(undefined, [])
  assert.ok(out)
  assert.equal(out.length, 2)
  const callId = stableCallId(undefined, 1n)
  assertPairShape(out[0], out[1], callId, text)
})

test('PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', () => {
  const raw = [userMsg('msg_1', text)]
  const out = inject('ses_quote', raw)
  assert.equal(isPairProgrammingThought(out[2]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 3)
  assert.equal(out[2].info.role, 'user')
})
