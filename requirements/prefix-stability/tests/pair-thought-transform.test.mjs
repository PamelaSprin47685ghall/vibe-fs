// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: prefix-stability. PPT: PairProgrammingThoughtTransform — HOST-013
// permanent pair injection: anchor/replay 机制（placement、replay、call-id
// 稳定性、skip-auto-injected 环境门、同 occurrence 恢复）。
// Cursor wire 渲染断言（NUL+BOM guidance）归 provider-projection；
// PAIR_HINT marker 正文 craft 归 cognitive-environment。

import assert from 'node:assert/strict'
import test from 'node:test'

import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const {
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
} = pair

const inject = async (session, raw, markerText = text) => {
  const result = await tryInject(session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
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
const skillContent = (markerText) => markerText

const assertPairShape = (msg, callId, markerText) => {
  assert.equal(msg.info.role, 'assistant')
  assert.equal(msg.info.source, source)
  assert.equal(msg.info.synthetic, true)
  assert.equal(msg.parts.length, 1)
  assert.equal(msg.parts[0].type, 'tool')
  assert.equal(msg.parts[0].tool, 'skill')
  assert.equal(msg.parts[0].callID, callId)
  assert.equal(msg.parts[0].state.status, 'completed')
  assert.notEqual(msg.parts[0].state.status, 'pending')
  assert.notEqual(msg.parts[0].state.status, 'running')
  assert.deepEqual(msg.parts[0].state.input, { name: '' })
  assert.equal(msg.parts[0].state.output, skillContent(markerText))
}

test('WHAT[PREFIX-STABILITY-014] PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-auto-injected')
  assert.ok(text.length > 0, 'frozen thought text must be non-empty')
  assert.equal(isPairProgrammingThought(null), false)
  assert.equal(isPairProgrammingThought({}), false)
  assert.equal(isPairProgrammingThought({ info: { source: 'other' } }), false)
  assert.equal(isPairProgrammingThought({ info: { source } }), true)
  assert.equal(isPairProgrammingThought({ parts: [] }), false, 'no info.source means not a marker')
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_empty_history_does_not_inject_pair', async () => {
  const out = await inject('ses_empty', [])
  assert.equal(out.length, 0, 'empty history must not inject pair')
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_single_user_message_does_not_inject_pair_to_prevent_tool_start', async () => {
  const raw = [userMsg('msg_1')]
  const out = await inject('ses_1', raw)
  assert.ok(out)
  assert.equal(out.length, 1)
  assert.deepEqual(out[0], raw[0], 'single user message stays intact without preceding tool call')
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_places_pair_before_trailing_user_with_prior_assistant', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('u2', 'steer')]
  const out = await inject('ses_assistant', raw)
  assert.ok(out)
  assert.equal(out.length, 4)
  assert.deepEqual(out[0], raw[0])
  assert.deepEqual(out[1], raw[1])
  const callId = stableCallId('ses_assistant', 1n)
  assertPairShape(out[2], callId, text)
  assert.deepEqual(out[3], raw[2], 'steer user remains after pair')
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_merges_into_tool_batches_before_user', async () => {
  const raw = [
    toolCall('c1', 'bash', 't1'),
    toolCall('c2', 'read', 't2'),
    toolResult('r1', 'bash', 't1', 'out1'),
    toolResult('r2', 'read', 't2', 'out2'),
    userMsg('u1', 'steer'),
  ]
  const out = await inject('ses_tools', raw)
  assert.ok(out)
  assert.equal(out.length, 6)
  const callId = stableCallId('ses_tools', 1n)

  assert.equal(out[0].parts[0].tool, 'bash')
  assert.equal(out[1].parts[0].tool, 'read')
  assert.equal(out[2].parts[0].tool, 'bash')
  assert.equal(out[2].parts[0].state.status, 'completed')
  assert.equal(out[3].parts[0].tool, 'read')
  assert.equal(out[3].parts[0].state.status, 'completed')
  assert.equal(out[4].parts[0].tool, 'skill')
  assert.equal(out[4].parts[0].state.status, 'completed')
  assert.equal(out[4].parts[0].callID, callId)
  assert.deepEqual(out[4].parts[0].state.input, { name: '' })
  assert.equal(out[4].parts[0].state.output, skillContent(text))
  assert.deepEqual(out[5], raw[4])
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_second_pass_of_same_placement_replays_existing_pair', async () => {
  const initial = [userMsg('u1'), assistantText('a1'), userMsg('u2')]
  const once = await inject('ses_append', initial)
  assert.ok(once)
  assert.equal(once.length, 4)

  // Same real transcript again (production raw carries the previous wire's
  // synthetic messages): same placement → replay only, no new pair.
  const twice = await inject('ses_append', once)
  assert.ok(twice)
  assert.equal(twice.length, 4, 'same placement must not append a second pair')
  assert.equal(pairMessages(twice).length, 1)

  const firstCall = stableCallId('ses_append', 1n)
  assertPairShape(twice[2], firstCall, text)
  assert.deepEqual(twice, once, 'replay must be byte-identical')
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_call_id_is_stable_per_session_and_ordinal', () => {
  assert.equal(stableCallId('ses_1', 1n), stableCallId('ses_1', 1n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_1', 2n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_2', 1n))
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_without_session_id_still_appends_stable_pair', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('u2')]
  const out = await inject(undefined, raw)
  assert.ok(out)
  assert.equal(out.length, 4)
  const callId = stableCallId(undefined, 1n)
  assertPairShape(out[2], callId, text)
})

test('WHAT[PREFIX-STABILITY-014] PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('msg_1', text)]
  const out = await inject('ses_quote', raw)
  assert.equal(isPairProgrammingThought(out[3]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 4)
  assert.equal(out[3].info.role, 'user')
})

test('WHAT[PREFIX-STABILITY-010] PPT_skip_auto_injected_env_blocks_new_pair_but_replays_history', async () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(skipAutoInjectedRequested(undefined), false)

    const session = 'ses_skip_env'
    const seeded = await inject(session, [userMsg('u1'), assistantText('a1'), userMsg('msg_u1')])
    assert.equal(pairMessages(seeded).length, 1)

    process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = '1'
    assert.equal(skipAutoInjectedRequested(undefined), true)

    const raw = [
      ...seeded,
      toolCall('msg_c1', 'bash', 'call_1'),
      toolResult('msg_r1', 'bash', 'call_1'),
      userMsg('msg_u2'),
    ]
    const out = await inject(session, raw)
    // Historical pair for placement Before(msg_u1) still replays; no new pair for Before(msg_u2).
    assert.equal(pairMessages(out).length, 1)
    assert.equal(out.length, 7)
    assert.equal(isPairProgrammingThought(out[2]), true)
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('WHAT[PREFIX-STABILITY-010] PPT_skip_auto_injected_env_keeps_empty_transcript_without_pair', async () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = '1'
    const out = await inject('ses_skip_empty', [])
    assert.equal(out.length, 0)
    assert.equal(pairMessages(out).length, 0)
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('WHAT[PREFIX-STABILITY-010] C_PH_ordinary_cursor_ordinary_suppresses_then_restores_same_occurrence', async () => {
  const session = 'ses_cursor_transition'
  const initial = [userMsg('u1'), assistantText('a1'), userMsg('u2')]
  const ordinary = await inject(session, initial)
  const ordinaryCallId = stableCallId(session, 1n)
  assert.equal(pairMessages(ordinary).length, 1)

  const cursorReal = [{
    info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
    parts: [{ type: 'text', text: 'hello' }],
  }]
  const cursor = await inject(session, cursorReal)
  assert.equal(pairMessages(cursor).length, 0)
  assert.deepEqual(cursor, cursorReal)

  const back = await inject(session, initial)
  assert.equal(pairMessages(back).length, 1)
  assertPairShape(back[2], ordinaryCallId, text)
})

test('WHAT[PREFIX-STABILITY-010] PPT_distiller_and_blogger_never_inject_pair_hint', async () => {
  const bloggerMsg = [
    userMsg('u1'),
    assistantText('a1'),
    { info: { id: 'u2', role: 'user', agent: 'fast-blogger' }, parts: [{ type: 'text', text: 'blog task' }] },
  ]
  const bloggerOut = await inject('ses_blogger', bloggerMsg)
  assert.equal(pairMessages(bloggerOut).length, 0)
  assert.deepEqual(bloggerOut, bloggerMsg)

  const distillerMsg = [
    userMsg('u3'),
    assistantText('a2'),
    { info: { id: 'u4', role: 'user', agent: 'fast-distiller' }, parts: [{ type: 'text', text: 'distill task' }] },
  ]
  const distillerOut = await inject('ses_distiller', distillerMsg)
  assert.equal(pairMessages(distillerOut).length, 0)
  assert.deepEqual(distillerOut, distillerMsg)
})
