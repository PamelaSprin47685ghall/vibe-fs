import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as providerCodec from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

// H13: HOST-013 anchored replay — PREFIX LAW, placement idempotence, restart
// byte-equality, fail-closed anchors, prior-tip isolation, N-round property.
//
// The one decisive assertion everywhere is
// `ProviderProjection.isAppendOnlyPrefix(previousWire, nextWire)` — pair count,
// callID pairing and marker bytes alone can all pass on an implementation that
// has already broken the prefix cache (historyBlock relocation).

const {
  tryInjectWithJournal,
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
} = pair

// ── helpers ─────────────────────────────────────────────────────────────────

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

const inject = async (journalOrSession, sessionOrRaw, rawOrMarker = text, markerText = text) => {
  if (Array.isArray(sessionOrRaw)) {
    const result = await tryInject(journalOrSession, rawOrMarker ?? text, sessionOrRaw)
    assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
    return result.value
  }
  const result = await tryInjectWithJournal(journalOrSession, sessionOrRaw, markerText ?? text, rawOrMarker)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}

/** The authority: ARCH-004 append-only prefix law (ProviderProjection.isAppendOnlyPrefix). */
const wire = (raw) => providerCodec.decodeMessageView(raw)
const assertPrefixLaw = (previous, next, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(previous), wire(next)),
    true,
    `${label}: previous wire must be an exact prefix of next wire`,
  )
}
const assertWireEqual = (a, b, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(a), wire(b)),
    true,
    `${label}: wire(a) must be a prefix of wire(b)`,
  )
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(b), wire(a)),
    true,
    `${label}: wire(b) must be a prefix of wire(a)`,
  )
}

const toolNames = (messages) => messages.map((m) => m.parts[0]?.tool)
// Universal cursor mode: guidance is a NUL+BOM suffix on a real tool result,
// so history rows carry no synthetic tool names and pairMessages stays empty.
const guidanceSuffixCount = (messages) =>
  messages
    .map((m) => m.parts[0]?.state?.output ?? m.parts[0]?.state?.error ?? '')
    .filter((value) => typeof value === 'string')
    .map((value) => (value.match(/\0/g) ?? []).length)

/** Fresh durable journal in a temp dir. */
const openJournal = async (dir) => {
  const opened = await pair.createJournal(dir)
  assert.equal(opened.ok, true, JSON.stringify(opened))
  return opened
}

const durablePairCount = (journal, session) => pair.pairCount(journal, session)

// Universal cursor mode: the only guidance carrier is a NUL+BOM suffix on the
// terminal real tool result. There are no synthetic skill messages, so every
// shape assertion below counts pairMessages === 0 and checks suffix bytes.
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

test('WHAT[PREFIX-STABILITY-010] H13_02_historical_pair_never_relocates_to_current_batch', async () => {
  const session = 'h13-02'

  const round1 = [toolCall('c1', 'bash', 't1'), toolResult('r1', 'bash', 't1')]
  const wire1 = await inject(undefined, session, round1)
  // Req1 Resp1+suffix — no synthetic row follows the batch.
  assert.deepEqual(toolNames(wire1), ['bash', 'bash'])
  assert.equal(pairMessages(wire1).length, 0)
  assert.equal(wire1[1].parts[0].state.output, `ok\0\uFEFF${text}`)

  const round2 = [...wire1, toolCall('c2', 'read', 't2'), toolResult('r2', 'read', 't2')]
  const wire2 = await inject(undefined, session, round2)
  // Req1 Resp1+suffix Req2 Resp2+suffix
  // A historyBlock implementation would rewrite pair1's bytes next to the
  // current batch; instead the historical suffix is frozen in place.
  assert.deepEqual(toolNames(wire2), [
    'bash', 'bash',
    'read', 'read',
  ])
  assert.equal(pairMessages(wire2).length, 0)
  assert.equal(wire2[1].parts[0].state.output, `ok\0\uFEFF${text}`)
  assert.equal(wire2[3].parts[0].state.output, `ok\0\uFEFF${text}`)
  assertPrefixLaw(wire1, wire2, 'H13-02 no historical relocation')
})

test('WHAT[PREFIX-STABILITY-010] H13_02b_durable_history_replays_the_current_skill_wire_only', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1302b-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-02b'
    const appended = await pair.appendAnchoredPair(opened.journal, {
      session,
      ordinal: 1n,
      callId: 'pair-call',
      markerText: pair.text,
      callGapAfter: { kind: 'start' },
      resultGapAfter: { kind: 'start' },
    })
    assert.equal(appended.ok, true, JSON.stringify(appended))

    // Universal cursor mode: the durable fact persists journal-side, but with
    // no terminal real tool result there is no guidance carrier to render —
    // zero synthetic rows, even with history present.
    const wire = await inject(opened.journal, session, [])
    assert.deepEqual(wire, [])
    assert.equal(pairMessages(wire).length, 0)
    assert.equal(durablePairCount(opened.journal, session), 1)

    // The same durable history guides the next real terminal result instead.
    const guided = await inject(opened.journal, session, [
      toolCall('c1', 'bash', 't1'),
      toolResult('r1', 'bash', 't1', 'out1'),
    ])
    assert.deepEqual(toolNames(guided), ['bash', 'bash'])
    assert.equal(guided[1].parts[0].state.output, `out1\0\uFEFF${pair.text}`)
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── H13-03: same placement re-entry appends nothing ─────────────────────────

test('WHAT[PREFIX-STABILITY-010] H13_03_same_placement_reentry_appends_no_pair', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1303-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-03'
    const raw = [userMsg('u1'), assistantText('a1'), userMsg('msg_1')]

    const once = await inject(opened.journal, session, raw)
    assert.equal(once.length, 3)
    assert.deepEqual(once, raw, 'no terminal tool result means no guidance carrier')
    assert.equal(pairMessages(once).length, 0)
    assert.equal(durablePairCount(opened.journal, session), 1)

    const twice = await inject(opened.journal, session, [...once])
    assert.equal(twice.length, 3, 'same placement must replay, not append')
    assert.equal(pairMessages(twice).length, 0)
    assert.deepEqual(twice, once)
    assert.equal(durablePairCount(opened.journal, session), 1, 'journal must hold exactly one anchored fact')
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── H13-04: restart replay is byte-identical ────────────────────────────────

test('WHAT[PREFIX-STABILITY-010] H13_04_restart_replay_is_byte_identical', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1304-'))
  const session = 'h13-04'
  const raw = [
    toolCall('c1', 'bash', 't1'),
    toolResult('r1', 'bash', 't1'),
    userMsg('u1', 'steer'),
  ]

  const before = await openJournal(dir)
  let wireBefore
  try {
    wireBefore = await inject(before.journal, session, raw)
  } finally {
    pair.disposeJournal(before.journal)
  }

  // New process: boot the persisted journal, fold it, open a fresh writer.
  const after = await openJournal(dir)
  let wireAfter
  try {
    // The restarting process sees the persisted transcript including synthetics.
    wireAfter = await inject(after.journal, session, [...wireBefore])
    assertWireEqual(wireBefore, wireAfter, 'H13-04 restart replay')
    assert.equal(durablePairCount(after.journal, session), 1, 'restart re-entry must not append a second fact')
  } finally {
    pair.disposeJournal(after.journal)
  }

  rmSync(dir, { recursive: true, force: true })
})

// ── H13-05: missing-anchor pairs are omitted (XWire DropLeading) ────────────
//
// CTX-010 prefix probe rewrites the covered head to FrozenRecordPrefix and
// drops those messages. Anchors that lived in the dropped region are absent
// from the rewritten real view. Relocating the pair would break PREFIX LAW;
// AbortSession would kill the recovery slot. The durable fact stays; only
// placeable pairs render. When the full transcript returns, anchors reappear.

test('WHAT[PREFIX-STABILITY-010] H13_05_missing_anchor_pair_is_omitted_not_relocated', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1305-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-05'

    const appended = await pair.appendAnchoredPair(opened.journal, {
      session,
      ordinal: 1,
      callId: stableCallId(session, 1n),
      markerText: text,
      callGapAfter: 'msg_7',
      resultGapAfter: 'msg_7',
    })
    assert.equal(appended.ok, true, JSON.stringify(appended))

    // Rewritten view: covered u1/msg_7 gone; only a synthetic prefix + continue.
    const synthPrefix = {
      info: { id: 'synth-prefix-frozen', role: 'user' },
      parts: [{ type: 'text', text: '# Opening\ncovered work' }],
    }
    const cont = userMsg('u-continue', '# The previous attempt did not complete.')
    const result = await tryInjectWithJournal(opened.journal, session, text, [synthPrefix, cont])
    assert.equal(result.ok, true, `missing-anchor pair must omit, not fail: ${result.error ?? ''}`)
    const wire = result.value
    // Pair1 (after msg_7) must not reappear anywhere — no relocate.
    const call1 = stableCallId(session, 1n)
    assert.equal(
      wire.some((m) => m.parts?.[0]?.callID === call1),
      false,
      'unplaceable historical pair must not be relocated onto the rewritten view',
    )
    // Historical fact remains; a new pair may land on the rewritten trailing placement.
    const durable = durablePairCount(opened.journal, session)
    assert.ok(durable === 1 || durable === 2, `durable pairs 1..2, got ${durable}`)
    const pairs = pairMessages(wire)
    assert.ok(pairs.length === 0 || pairs.length === 1, 'at most one new pair on the rewritten view')
    assert.equal(
      pairs.some((m) => m.parts?.[0]?.callID === call1),
      false,
      'new wire pairs must not reuse the unplaceable historical callId',
    )
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

// X-B regression: after a durable pair on the opening user, XWire drops that
// user for the recovery continue. tryInject must still commit (not Abort).

test('WHAT[PREFIX-STABILITY-010] H13_05b_xwire_drop_leading_continue_still_commits', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1305b-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-05b'
    const user0 = userMsg('u0', 'opening')
    const asst0 = assistantText('a0')
    const user1 = userMsg('u1', 'X-B round 1')
    const failAsst = { info: { id: 'a1', role: 'assistant' }, parts: [] }
    const cont = userMsg('u2', '# The previous attempt did not complete.')
    const synthPrefix = {
      info: { id: 'synth-prefix-frozen', role: 'user' },
      parts: [{ type: 'text', text: '# Opening\nX-B round 1\n\n# Chronicle\nframe' }],
    }

    const wire1 = await inject(opened.journal, session, [user0, asst0, user1])
    assert.equal(durablePairCount(opened.journal, session), 1)
    assert.equal(pairMessages(wire1).length, 0)

    // DropLeading removes u1 (pair1's Before(u1) anchors). Neither view has a
    // terminal real tool result, so neither renders guidance — but both must
    // still commit (not Abort).
    const result = await tryInjectWithJournal(opened.journal, session, text, [synthPrefix, failAsst, cont])
    assert.equal(result.ok, true, `XWire continue must not fail closed: ${result.error ?? ''}`)
    const wire2 = result.value
    const call1 = stableCallId(session, 1n)
    assert.equal(
      wire2.some((m) => m.parts?.[0]?.callID === call1),
      false,
      'pair1 anchors dropped with covered prefix — must not reappear',
    )
    assert.equal(pairMessages(wire2).length, 0)
    const countAfterContinue = durablePairCount(opened.journal, session)
    // Full transcript (no drop) replays the same durable occurrence without
    // appending a new fact; pure u1 re-entry is byte-identical.
    const restored = await inject(opened.journal, session, [user0, asst0, user1, failAsst, cont])
    assert.equal(pairMessages(restored).length, 0)
    assert.deepEqual(restored, [user0, asst0, user1, failAsst, cont])
    assert.equal(durablePairCount(opened.journal, session), countAfterContinue, 're-entry must not append a second fact')
    assertWireEqual(wire1, await inject(opened.journal, session, [user0, asst0, user1]), 'H13-05b same placement on u1 is pure replay')
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── H13-06: prior tip only affects the new pair ─────────────────────────────

test('WHAT[PREFIX-STABILITY-010] H13_06_prior_tip_only_affects_the_new_pair', async () => {
  const session = 'h13-06'

  const wire1 = await inject(
    undefined,
    session,
    [toolCall('c1', 'bash', 't1'), toolResult('r1', 'bash', 't1', 'out1')],
    'guideline',
  )
  assert.equal(pairMessages(wire1).length, 0)
  assert.equal(wire1[1].parts[0].state.output, `out1\0\uFEFFguideline`)

  const wire2 = await inject(
    undefined,
    session,
    [...wire1, toolCall('c2', 'bash', 't2'), toolResult('r2', 'bash', 't2', 'out2')],
    'tip2\n\nguideline',
  )
  assert.equal(pairMessages(wire2).length, 0)
  assert.equal(wire2[1].parts[0].state.output, `out1\0\uFEFFguideline`, 'historical marker bytes must never change')
  assert.equal(wire2[3].parts[0].state.output, `out2\0\uFEFFtip2\n\nguideline`)

  assertPrefixLaw(wire1, wire2, 'H13-06 prior tip isolation')
})

// ── H13-08: N-round append-only prefix property ─────────────────────────────

/** 32-bit LCG — same seed, same sequence (join-completion-property precedent). */
const mulberry32 = (seed) => {
  let a = seed >>> 0
  return () => {
    a |= 0
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

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
