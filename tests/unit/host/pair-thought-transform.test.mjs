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

const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))

const assertPairShape = (callMsg, resultMsg, callId, markerText) => {
  assert.equal(callMsg.info.role, 'assistant')
  assert.equal(callMsg.info.source, source)
  assert.equal(callMsg.info.synthetic, true)
  assert.equal(callMsg.parts.length, 1)
  assert.equal(callMsg.parts[0].type, 'tool')
  assert.equal(callMsg.parts[0].tool, 'guideline')
  assert.equal(callMsg.parts[0].callID, callId)
  assert.equal(callMsg.parts[0].state.status, 'pending')

  assert.equal(resultMsg.info.role, 'assistant')
  assert.equal(resultMsg.info.source, source)
  assert.equal(resultMsg.info.synthetic, true)
  assert.equal(resultMsg.parts[0].type, 'tool')
  assert.equal(resultMsg.parts[0].tool, 'guideline')
  assert.equal(resultMsg.parts[0].callID, callId)
  assert.equal(resultMsg.parts[0].state.status, 'completed')
  assert.equal(resultMsg.parts[0].state.output, markerText)
}

test('PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-guideline')
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

test('PPT_tryInject_appends_pair_after_user_message', () => {
  const raw = [userMsg('msg_1')]
  const out = inject('ses_1', raw)
  assert.ok(out)
  assert.equal(out.length, 3)
  assert.deepEqual(out[0], raw[0], 'original user message must pass through verbatim')
  const callId = stableCallId('ses_1', 1n)
  assertPairShape(out[1], out[2], callId, text)
})

test('PPT_tryInject_appends_pair_after_assistant_only_history', () => {
  const raw = [assistantText('m1')]
  const out = inject('ses_assistant', raw)
  assert.ok(out)
  assert.equal(out.length, 3)
  assert.deepEqual(out[0], raw[0])
  assert.equal(pairMessages(out).length, 2)
})

test('PPT_tryInject_second_pass_appends_second_pair_and_keeps_first', () => {
  const once = inject('ses_append', [userMsg('msg_1')])
  assert.ok(once)
  assert.equal(once.length, 3)

  // Pass non-pair base again: durable/memory history restores previous pair,
  // then appends the next ordinal.
  const twice = inject('ses_append', [userMsg('msg_1')])
  assert.ok(twice)
  assert.equal(twice.length, 5, 'history pair + new pair = 4 synthetic + 1 user')
  assert.deepEqual(twice[0], once[0])
  assert.equal(pairMessages(twice).length, 4)

  const firstCall = stableCallId('ses_append', 1n)
  const secondCall = stableCallId('ses_append', 2n)
  assert.notEqual(firstCall, secondCall)
  assertPairShape(twice[1], twice[2], firstCall, text)
  assertPairShape(twice[3], twice[4], secondCall, text)
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
  assert.equal(isPairProgrammingThought(out[0]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 3)
})
