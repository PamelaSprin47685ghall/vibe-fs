// H13: HOST-013 anchored replay — PREFIX LAW, placement idempotence, restart
// byte-equality, fail-closed anchors, prior-tip isolation, N-round property.
//
// The one decisive assertion everywhere is
// `ProviderProjection.isAppendOnlyPrefix(previousWire, nextWire)` — pair count,
// callID pairing and marker bytes alone can all pass on an implementation that
// has already broken the prefix cache (historyBlock relocation).

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as providerCodec from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

const {
  tryInjectWithJournal,
  isPairProgrammingThought,
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

const inject = async (journal, session, raw, markerText = text) => {
  const result = await tryInjectWithJournal(journal, session, markerText, raw)
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

// ── H13-01: the canonical multi-tool sequence ───────────────────────────────

test('WHAT[PREFIX-STABILITY-001] H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix', async () => {
  const session = 'h13-01'
  const round1Real = [
    toolCall('c1', 'bash', 't1'),
    toolCall('c2', 'read', 't2'),
    toolResult('r1', 'bash', 't1'),
    toolResult('r2', 'read', 't2'),
  ]

  // round 1: Req1 Req2 Resp1 Resp2 — universal cursor mode carries guidance
  // only as a NUL+BOM suffix on the terminal real tool result (Resp2).
  const round1Wire = await inject(undefined, session, round1Real)
  assert.deepEqual(toolNames(round1Wire), ['bash', 'read', 'bash', 'read'])
  assert.equal(pairMessages(round1Wire).length, 0)
  assert.equal(round1Wire[3].parts[0].state.status, 'completed')
  assert.notEqual(round1Wire[3].parts[0].state.status, 'pending')
  assert.equal(round1Wire[3].parts[0].state.output, `ok\0\uFEFF${text}`)
  assert.equal(round1Wire[2].parts[0].state.output, 'ok')

  // round 2 input carries the previous wire's suffixed bytes (Host persists
  // them): Req1 Req2 Resp1 Resp2+suffix Req3 Resp3+suffix
  const round2Real = [
    ...round1Wire,
    toolCall('c3', 'write', 't3'),
    toolResult('r3', 'write', 't3'),
  ]
  const round2Wire = await inject(undefined, session, round2Real)
  assert.deepEqual(toolNames(round2Wire), [
    'bash', 'read', 'bash', 'read',
    'write', 'write',
  ])
  assert.equal(pairMessages(round2Wire).length, 0)
  assert.equal(round2Wire[3].parts[0].state.output, `ok\0\uFEFF${text}`, 'historical guidance bytes never change')
  assert.equal(round2Wire[5].parts[0].state.output, `ok\0\uFEFF${text}`)

  assertPrefixLaw(round1Wire, round2Wire, 'H13-01 canonical sequence')
})

// ── H13-02: history never relocates with the current placement ──────────────

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

test('WHAT[PREFIX-STABILITY-001] H13_08_n_round_property_prefix_law_holds', async () => {  const rand = mulberry32(0x1357)
  const session = 'h13-08'
  const rounds = 8

  // history grows exactly as the Host transcript does: previous wire (result
  // guidance bytes included) + the new real content of this round.
  let history = []
  let previousWire
  let previousPairCount = 0

  for (let n = 1; n <= rounds; n++) {
    const fresh = []
    if (rand() < 0.25) {
      // no-tool turn: assistant text only
      fresh.push(assistantText(`a${n}`))
    } else {
      const toolCount = 1 + Math.floor(rand() * 5)
      for (let i = 0; i < toolCount; i++) fresh.push(toolCall(`c${n}_${i}`, 'bash', `t${n}_${i}`))
      for (let i = 0; i < toolCount; i++) fresh.push(toolResult(`r${n}_${i}`, 'bash', `t${n}_${i}`))
    }
    if (rand() < 0.35) fresh.push(userMsg(`u${n}`))

    const wire = await inject(undefined, session, [...history, ...fresh])

    // Universal cursor mode emits zero synthetic rows; guidance replays strip
    // the suffix for placement and re-apply it exactly once — no row ever
    // accumulates a doubled NUL+BOM suffix.
    const pairCount = pairMessages(wire).length
    assert.ok(pairCount >= previousPairCount, `round ${n}: pair count must never shrink`)
    assert.ok(pairCount <= previousPairCount + 1, `round ${n}: at most one new pair per round`)
    for (const suffixes of guidanceSuffixCount(wire)) {
      assert.ok(suffixes <= 1, `round ${n}: at most one guidance suffix per result`)
    }

    if (previousWire !== undefined) {
      assertPrefixLaw(previousWire, wire, `H13-08 round ${n}`)
    }
    history = wire
    previousWire = wire
    previousPairCount = pairCount
  }
})

// ── H13 / PREFIX-STABILITY-014: pair 正文不进 trace 系 ──────────────────────
//
// HOST-013 行为约束 4：pair 正文不得进入 XTrace / Companion decode / Blogger
// delta / work record / compaction input；仅 pair 的 durable 投影事实
// （PairProgrammingGuidelineAnchored → Guidelines）参与 HOST-013 恢复。
// 行为断言：fold 一个 anchored pair 事实只长出 Guidelines，XTrace / Blog /
// 其它 trace 系投影保持不存在（None = undefined）。

test('WHAT[PREFIX-STABILITY-014] PREFIX_STABILITY_pair_body_stays_out_of_the_trace_projections', () => {
  const session = 'h13-014'
  const markerBody = 'SECRET synthetic marker body'
  const result = pair.foldAnchoredPair({
    session,
    ordinal: 1,
    callId: stableCallId(session, 1n),
    markerText: markerBody,
    callGapAfter: 'msg_7',
    resultGapAfter: 'msg_7',
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result))

  const flags = result.flags
  // The only projection the pair fact may grow is Guidelines (HOST-013 recovery).
  assert.equal(flags.guidelines, true, 'the anchored fact must land in Guidelines')

  // HOST-013 行为约束 4: no trace-family projection may be created or touched.
  // (LifecycleWorkRecord is materialized FROM the XTrace, so an absent XTrace
  // also means the work record can never carry pair bytes.)
  assert.equal(flags.xTrace, false, 'XTrace must not be created by the pair fact')
  assert.equal(flags.blog, false, 'Companion frame sequence must not be created by the pair fact')
})
