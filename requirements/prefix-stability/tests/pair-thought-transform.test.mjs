// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: prefix-stability. PPT: PairProgrammingThoughtTransform — HOST-013
// universal cursor mode: zero synthetic skill messages on every provider.
// Guidance travels only as a NUL+BOM suffix on the terminal real tool result;
// the durable occurrence (ordinal/call-id 稳定性、skip-auto-injected 环境门、
// 同 occurrence 恢复、replay 去重) is journal-level, never a wire message.
// NUL+BOM error-result 断言归 provider-projection；
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
// Universal cursor mode: the only guidance carrier is a NUL+BOM suffix on the
// terminal real tool result. There are no synthetic skill messages, so every
// shape assertion below counts pairMessages === 0 and checks suffix bytes.
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

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
  // Universal cursor mode: no terminal real tool result exists, so no guidance
  // carrier exists either — the transcript passes through byte-identical with
  // zero synthetic messages. The durable occurrence lives in the journal.
  assert.equal(out.length, 3)
  assert.deepEqual(out, raw)
  assert.equal(pairMessages(out).length, 0)
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
  // Universal cursor mode: the batch keeps its four real rows plus the steer
  // user; guidance lands on the terminal real tool result only.
  assert.equal(out.length, 5)
  assert.equal(pairMessages(out).length, 0)

  assert.equal(out[0].parts[0].tool, 'bash')
  assert.equal(out[1].parts[0].tool, 'read')
  assert.equal(out[2].parts[0].tool, 'bash')
  assert.equal(out[2].parts[0].state.status, 'completed')
  assert.equal(out[2].parts[0].state.output, 'out1')
  assert.equal(out[3].parts[0].tool, 'read')
  assert.equal(out[3].parts[0].state.status, 'completed')
  assert.equal(out[3].parts[0].state.output, `out2${guidanceSuffix(text)}`)
  assert.deepEqual(out[4], raw[4], 'steer user remains the terminal row')

  // Re-feeding the wire strips the suffix for placement, then re-applies it:
  // replay is byte-identical, never a doubled suffix.
  const replay = await inject('ses_tools', out)
  assert.deepEqual(replay, out)
  assert.equal(terminalOutputOf(replay, 'r2'), `out2${guidanceSuffix(text)}`)
})

test('WHAT[PREFIX-STABILITY-010] PPT_tryInject_second_pass_of_same_placement_replays_existing_pair', async () => {
  const initial = [userMsg('u1'), assistantText('a1'), userMsg('u2')]
  const once = await inject('ses_append', initial)
  assert.ok(once)
  assert.equal(once.length, 3)
  assert.equal(pairMessages(once).length, 0)

  // Same real transcript again: same placement → replay only, no new guidance
  // carrier. The durable occurrence is journal-level, never a wire message.
  const twice = await inject('ses_append', once)
  assert.ok(twice)
  assert.equal(twice.length, 3, 'same placement must not append a second carrier')
  assert.equal(pairMessages(twice).length, 0)
  assert.deepEqual(twice, once, 'replay must be byte-identical')
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_call_id_is_stable_per_session_and_ordinal', () => {
  assert.equal(stableCallId('ses_1', 1n), stableCallId('ses_1', 1n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_1', 2n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_2', 1n))
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_without_session_id_still_appends_stable_pair', async () => {
  // Without a session id the guidance still lands deterministically on the
  // terminal real tool result; a session-less trailing-user turn passes through.
  const raw = [toolCall('c1', 'bash', 't1'), toolResult('r1', 'bash', 't1', 'out1')]
  const out = await inject(undefined, raw)
  assert.ok(out)
  assert.equal(out.length, 2)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(terminalOutputOf(out, 'r1'), `out1${guidanceSuffix(text)}`)
  assert.deepEqual(await inject(undefined, out), out, 'session-less replay stays byte-identical')
})

test('WHAT[PREFIX-STABILITY-014] PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('msg_1', text)]
  const out = await inject('ses_quote', raw)
  assert.equal(isPairProgrammingThought(out[2]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 3)
  assert.equal(out[2].info.role, 'user')
})

test('WHAT[PREFIX-STABILITY-010] PPT_skip_auto_injected_env_blocks_new_pair_but_replays_history', async () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(skipAutoInjectedRequested(undefined), false)

    const session = 'ses_skip_env'
    const seeded = await inject(session, [
      toolCall('msg_c0', 'bash', 'call_0'),
      toolResult('msg_r0', 'bash', 'call_0', 'out0'),
      userMsg('msg_u1'),
    ])
    assert.equal(pairMessages(seeded).length, 0)
    assert.equal(terminalOutputOf(seeded, 'msg_r0'), `out0${guidanceSuffix(text)}`)

    process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = '1'
    assert.equal(skipAutoInjectedRequested(undefined), true)

    // Historical guidance bytes replay untouched; the new terminal result gets
    // no fresh suffix while the env gate is set.
    const replay = await inject(session, [...seeded])
    assert.deepEqual(replay, seeded, 'history replays byte-identical under the skip gate')

    const raw = [
      ...seeded,
      toolCall('msg_c1', 'bash', 'call_1'),
      toolResult('msg_r1', 'bash', 'call_1', 'out1'),
      userMsg('msg_u2'),
    ]
    const out = await inject(session, raw)
    assert.equal(pairMessages(out).length, 0)
    assert.equal(out.length, 6)
    assert.equal(terminalOutputOf(out, 'msg_r0'), `out0${guidanceSuffix(text)}`)
    assert.equal(terminalOutputOf(out, 'msg_r1'), 'out1')
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
  // Universal cursor mode: neither ordinary nor cursor turns emit synthetic
  // messages; the durable occurrence is journal-level and the wire passes
  // through. Returning to the ordinary turn restores the identical wire.
  assert.equal(pairMessages(ordinary).length, 0)
  assert.deepEqual(ordinary, initial)

  const cursorReal = [{
    info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
    parts: [{ type: 'text', text: 'hello' }],
  }]
  const cursor = await inject(session, cursorReal)
  assert.equal(pairMessages(cursor).length, 0)
  assert.deepEqual(cursor, cursorReal)

  const back = await inject(session, initial)
  assert.equal(pairMessages(back).length, 0)
  assert.deepEqual(back, ordinary, 'same occurrence restores the identical wire')
})

test('WHAT[PREFIX-STABILITY-010] PPT_distiller_and_blogger_never_inject_pair_hint', async () => {
  const bloggerMsg = [
    userMsg('u1'),
    assistantText('a1'),
    { info: { id: 'u2', role: 'user', agent: 'blogger' }, parts: [{ type: 'text', text: 'blog task' }] },
  ]
  const bloggerOut = await inject('ses_blogger', bloggerMsg)
  assert.equal(pairMessages(bloggerOut).length, 0)
  assert.deepEqual(bloggerOut, bloggerMsg)

  const distillerMsg = [
    userMsg('u3'),
    assistantText('a2'),
    { info: { id: 'u4', role: 'user', agent: 'distiller' }, parts: [{ type: 'text', text: 'distill task' }] },
  ]
  const distillerOut = await inject('ses_distiller', distillerMsg)
  assert.equal(pairMessages(distillerOut).length, 0)
  assert.deepEqual(distillerOut, distillerMsg)

  // Suppression holds even with a terminal real tool result: no guidance
  // suffix is appended for the canonical blogger/distiller turns.
  const bloggerTools = [
    toolCall('c1', 'bash', 't1'),
    toolResult('r1', 'bash', 't1', 'out1'),
    { info: { id: 'u2', role: 'user', agent: 'blogger' }, parts: [{ type: 'text', text: 'blog task' }] },
  ]
  const bloggerToolsOut = await inject('ses_blogger_tools', bloggerTools)
  assert.equal(pairMessages(bloggerToolsOut).length, 0)
  assert.deepEqual(bloggerToolsOut, bloggerTools)
})
