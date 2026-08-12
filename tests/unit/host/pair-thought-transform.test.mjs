// PPT: PairProgrammingThoughtTransform — HOST-013 permanent pair injection.

import assert from 'node:assert/strict'
import test from 'node:test'

import { toList, listItems, resultOf } from '../support/domain.mjs'

const {
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
  stableCursorMessageId,
  tryInjectWithCursorRole,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

const inject = (session, raw, markerText = text) => {
  const result = resultOf(tryInject(undefined, session, markerText, toList(raw)))
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return listItems(result.value)
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

test('PPT_tryInject_appends_pair_on_empty_history_at_start_gap', () => {
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

test('PPT_tryInject_second_pass_of_same_placement_replays_existing_pair', () => {
  const once = inject('ses_append', [userMsg('msg_1')])
  assert.ok(once)
  assert.equal(once.length, 3)

  // Same real transcript again (production raw carries the previous wire's
  // synthetic messages): same placement → replay only, no new pair.
  const twice = inject('ses_append', once)
  assert.ok(twice)
  assert.equal(twice.length, 3, 'same placement must not append a second pair')
  assert.equal(pairMessages(twice).length, 2)

  const firstCall = stableCallId('ses_append', 1n)
  assertPairShape(twice[0], twice[1], firstCall, text)
  assert.equal(twice[2].info.role, 'user')
  assert.deepEqual(twice, once, 'replay must be byte-identical')
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

test('PPT_skip_auto_injected_env_blocks_new_pair_but_replays_history', () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(skipAutoInjectedRequested(undefined), false)

    const session = 'ses_skip_env'
    const seeded = inject(session, [userMsg('msg_u1')])
    assert.equal(pairMessages(seeded).length, 2)

    process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = '1'
    assert.equal(skipAutoInjectedRequested(undefined), true)

    const raw = [
      userMsg('msg_u1'),
      toolCall('msg_c1', 'bash', 'call_1'),
      toolResult('msg_r1', 'bash', 'call_1'),
      userMsg('msg_u2'),
    ]
    const out = inject(session, raw)
    // Historical pair for placement Before(msg_u1) still replays; no new pair for Before(msg_u2).
    assert.equal(pairMessages(out).length, 2)
    assert.equal(out.length, 6)
    assert.equal(isPairProgrammingThought(out[0]), true)
    assert.equal(isPairProgrammingThought(out[1]), true)
    assert.equal(out[2].info.id, 'msg_u1')
    assert.equal(out[5].info.id, 'msg_u2')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('PPT_skip_auto_injected_env_keeps_empty_transcript_without_pair', () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = '1'
    const out = inject('ses_skip_empty', [])
    assert.equal(out.length, 0)
    assert.equal(pairMessages(out).length, 0)
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('C_PH_cursor_is_projection_not_skip_and_uses_single_assistant_text', () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(skipAutoInjectedRequested(undefined), false)
    assert.equal(skipAutoInjectedRequested('cursor'), false)
    assert.equal(skipAutoInjectedRequested('anthropic'), false)

    const raw = [{
      info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
      parts: [{ type: 'text', text: 'steer' }],
    }]
    const out = inject('ses_cursor_text', raw)
    const marker = pairMessages(out)
    assert.equal(marker.length, 1, 'Cursor occurrence is one text message, not a fake-tool pair')
    assert.equal(marker[0].info.role, 'assistant')
    assert.equal(marker[0].parts[0].type, 'text')
    assert.equal(marker[0].parts[0].text, text)
    assert.equal(marker[0].info.id, stableCursorMessageId(stableCallId('ses_cursor_text', 1n), 'assistant'))
    assert.equal(marker[0].parts.some((p) => p.tool === 'auto-injected'), false)
    assert.equal(out.at(-1).info.id, 'u1')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('C_PH_three_cursor_encoders_change_only_role_and_stable_id', () => {
  const raw = [{
    info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
    parts: [{ type: 'text', text: 'work' }],
  }]
  const session = 'ses_cursor_modes'
  for (const role of ['assistant', 'user', 'system']) {
    const result = resultOf(tryInjectWithCursorRole(undefined, session, text, toList(raw), role))
    assert.equal(result.ok, true)
    const marker = pairMessages(listItems(result.value))
    assert.equal(marker.length, 1)
    assert.equal(marker[0].info.role, role)
    assert.equal(marker[0].parts[0].type, 'text')
    assert.equal(marker[0].parts[0].text, text)
    assert.equal(marker[0].info.id, stableCursorMessageId(stableCallId(session, 1n), role))
  }
})

test('C_PH_ordinary_cursor_ordinary_reprojects_same_occurrence', () => {
  const session = 'ses_cursor_transition'
  const ordinary = inject(session, [userMsg('u1')])
  const ordinaryCallId = stableCallId(session, 1n)
  assert.equal(pairMessages(ordinary).length, 2)

  const cursorReal = [{
    info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
    parts: [{ type: 'text', text: 'hello' }],
  }]
  const cursor = inject(session, cursorReal)
  const cursorMarkers = pairMessages(cursor)
  assert.equal(cursorMarkers.length, 1)
  assert.equal(cursorMarkers[0].info.id, stableCursorMessageId(ordinaryCallId, 'assistant'))

  const back = inject(session, [userMsg('u1')])
  assert.equal(pairMessages(back).length, 2)
  assertPairShape(back[0], back[1], ordinaryCallId, text)
})

test('PAIR_HINT_canonical_text_encourages_needhelp_and_parallel_wave_without_global_N', () => {
  assert.match(text, /\[NEEDHELP\]/)
  assert.match(text, /并行|parallel/i)
  assert.match(text, /依赖|dependenc/i)
  assert.doesNotMatch(text, /最多\s*\d+|max(?:imum)?\s+\d+/i)
})
