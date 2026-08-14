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

import {
  agentFact,
  agentJournal,
  bootSnapshot,
  caseOf,
  fold,
  listItems,
  payloadOf,
  providerProjection,
  resultOf,
  sessionId,
  stream,
  toolCallId,
  transcriptAddress,
  transcriptGap,
  toList,
} from '../../verification-system/tests/support/domain.mjs'

const {
  tryInject,
  isPairProgrammingThought,
  source,
  text,
  stableCallId,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

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
  const result = resultOf(await tryInject(journal, session, markerText, toList(raw)))
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return listItems(result.value)
}

/** The authority: ARCH-004 append-only prefix law (ProviderProjection.isAppendOnlyPrefix). */
const wire = (raw) => providerProjection.decodeMessageView(toList(raw))
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
const callIdOf = (messages, index) => messages[index].parts[0].callID

/** Fresh durable journal in a temp dir. */
const openJournal = async (dir) => {
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, JSON.stringify(opened))
  return opened
}

const durablePairCount = (journal, session) => {
  const snapshot = agentJournal.snapshot(journal)
  const sessionProj = fold.session(snapshot, session)
  assert.ok(sessionProj, `session ${session} must exist in the journal`)
  const pairs = listItems(sessionProj.Guidelines.Pairs)
  return pairs.length
}

// ── H13-01: the canonical multi-tool sequence ───────────────────────────────

test('H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix', async () => {
  const session = 'h13-01'
  const round1Real = [
    toolCall('c1', 'bash', 't1'),
    toolCall('c2', 'read', 't2'),
    toolResult('r1', 'bash', 't1'),
    toolResult('r2', 'read', 't2'),
  ]

  // round 1: Req1 Req2 Resp1 Resp2 → FakePair1 (one completed Host row)
  const round1Wire = await inject(undefined, session, round1Real)
  assert.deepEqual(toolNames(round1Wire), ['bash', 'read', 'bash', 'read', 'auto-injected'])
  const call1 = stableCallId(session, 1n)
  assert.equal(round1Wire[4].parts[0].state.status, 'completed')
  assert.notEqual(round1Wire[4].parts[0].state.status, 'pending')
  assert.equal(callIdOf(round1Wire, 4), call1)

  // round 2 input carries the previous wire's synthetic messages (Host persists
  // them): Req1 Req2 Resp1 Resp2 FakePair1 Req3 Resp3
  const round2Real = [
    ...round1Wire,
    toolCall('c3', 'write', 't3'),
    toolResult('r3', 'write', 't3'),
  ]
  const round2Wire = await inject(undefined, session, round2Real)
  assert.deepEqual(toolNames(round2Wire), [
    'bash', 'read', 'bash', 'read', 'auto-injected',
    'write', 'write', 'auto-injected',
  ])
  const call2 = stableCallId(session, 2n)
  assert.equal(callIdOf(round2Wire, 7), call2)
  assert.notEqual(call1, call2)

  assertPrefixLaw(round1Wire, round2Wire, 'H13-01 canonical sequence')
})

// ── H13-02: history never relocates with the current placement ──────────────

test('H13_02_historical_pair_never_relocates_to_current_batch', async () => {
  const session = 'h13-02'

  const round1 = [toolCall('c1', 'bash', 't1'), toolResult('r1', 'bash', 't1')]
  const wire1 = await inject(undefined, session, round1)
  // Req1 Resp1 FakePair1
  assert.deepEqual(toolNames(wire1), ['bash', 'bash', 'auto-injected'])

  const round2 = [...wire1, toolCall('c2', 'read', 't2'), toolResult('r2', 'read', 't2')]
  const wire2 = await inject(undefined, session, round2)
  // Req1 Resp1 FakePair1 Req2 Resp2 FakePair2
  // A historyBlock implementation would move pair1 next to the current batch.
  assert.deepEqual(toolNames(wire2), [
    'bash', 'bash', 'auto-injected',
    'read', 'read', 'auto-injected',
  ])
  assertPrefixLaw(wire1, wire2, 'H13-02 no historical relocation')
})

// ── H13-03: same placement re-entry appends nothing ─────────────────────────

test('H13_03_same_placement_reentry_appends_no_pair', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1303-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-03'
    const raw = [userMsg('msg_1')]

    const once = await inject(opened.journal, session, raw)
    assert.equal(once.length, 2)
    assert.equal(durablePairCount(opened.journal, session), 1)

    const twice = await inject(opened.journal, session, [...once])
    assert.equal(twice.length, 2, 'same placement must replay, not append')
    assert.equal(pairMessages(twice).length, 1)
    assert.deepEqual(twice, once)
    assert.equal(durablePairCount(opened.journal, session), 1, 'journal must hold exactly one anchored fact')
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── H13-04: restart replay is byte-identical ────────────────────────────────

test('H13_04_restart_replay_is_byte_identical', async () => {
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
    before.dispose()
  }

  // New process: boot the persisted journal, fold it, open a fresh writer.
  const boot = await bootSnapshot.load(dir)
  const after = await agentJournal.createFromBoot({ directory: dir, boot })
  assert.equal(after.ok, true, JSON.stringify(after))
  let wireAfter
  try {
    // The restarting process sees the persisted transcript including synthetics.
    wireAfter = await inject(after.journal, session, [...wireBefore])
  } finally {
    after.dispose()
  }

  assertWireEqual(wireBefore, wireAfter, 'H13-04 restart replay')
  assert.equal(durablePairCount(after.journal, session), 1, 'restart re-entry must not append a second fact')
  rmSync(dir, { recursive: true, force: true })
})

// ── H13-05: missing-anchor pairs are omitted (XWire DropLeading) ────────────
//
// CTX-010 prefix probe rewrites the covered head to FrozenRecordPrefix and
// drops those messages. Anchors that lived in the dropped region are absent
// from the rewritten real view. Relocating the pair would break PREFIX LAW;
// AbortSession would kill the recovery slot. The durable fact stays; only
// placeable pairs render. When the full transcript returns, anchors reappear.

test('H13_05_missing_anchor_pair_is_omitted_not_relocated', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1305-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-05'

    const anchored = agentFact('PairProgrammingGuidelineAnchored', {
      SessionId: sessionId(session),
      Ordinal: 1n,
      CallId: toolCallId(stableCallId(session, 1n)),
      MarkerText: text,
      CallGap: transcriptGap.after(transcriptAddress.create('msg_7')),
      ResultGap: transcriptGap.after(transcriptAddress.create('msg_7')),
    })
    const appended = await agentJournal.appendAgent(
      stream.session(sessionId(session)),
      undefined,
      anchored,
      opened.journal,
    )
    assert.equal(appended.ok, true, JSON.stringify(appended))

    // Rewritten view: covered u1/msg_7 gone; only a synthetic prefix + continue.
    const synthPrefix = {
      info: { id: 'synth-prefix-frozen', role: 'user' },
      parts: [{ type: 'text', text: '# Opening\ncovered work' }],
    }
    const cont = userMsg('u-continue', '# The previous attempt did not complete.')
    const result = resultOf(await tryInject(opened.journal, session, text, toList([synthPrefix, cont])))
    assert.equal(result.ok, true, `missing-anchor pair must omit, not fail: ${result.error ?? ''}`)
    const wire = listItems(result.value)
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
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

// X-B regression: after a durable pair on the opening user, XWire drops that
// user for the recovery continue. tryInject must still commit (not Abort).
test('H13_05b_xwire_drop_leading_continue_still_commits', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-h1305b-'))
  const opened = await openJournal(dir)
  try {
    const session = 'h13-05b'
    const user1 = userMsg('u1', 'X-B round 1')
    const failAsst = { info: { id: 'a1', role: 'assistant' }, parts: [] }
    const cont = userMsg('u2', '# The previous attempt did not complete.')
    const synthPrefix = {
      info: { id: 'synth-prefix-frozen', role: 'user' },
      parts: [{ type: 'text', text: '# Opening\nX-B round 1\n\n# Chronicle\nframe' }],
    }

    const wire1 = await inject(opened.journal, session, [user1])
    assert.equal(durablePairCount(opened.journal, session), 1)

    // DropLeading removes u1 (pair1's Before(u1) anchors).
    const result = resultOf(
      await tryInject(opened.journal, session, text, toList([synthPrefix, failAsst, cont])),
    )
    assert.equal(result.ok, true, `XWire continue must not fail closed: ${result.error ?? ''}`)
    const wire2 = listItems(result.value)
    const call1 = stableCallId(session, 1n)
    assert.equal(
      wire2.some((m) => m.parts?.[0]?.callID === call1),
      false,
      'pair1 anchors dropped with covered prefix — must not reappear',
    )
    // Full transcript (no drop) still replays pair1; pure u1 re-entry is byte-identical.
    const restored = await inject(opened.journal, session, [user1, failAsst, cont])
    assert.ok(
      restored.some((m) => m.parts?.[0]?.callID === call1),
      'full transcript must re-place pair1 at its durable anchor',
    )
    assertWireEqual(wire1, await inject(opened.journal, session, [user1]), 'H13-05b same placement on u1 is pure replay')
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── H13-06: prior tip only affects the new pair ─────────────────────────────

test('H13_06_prior_tip_only_affects_the_new_pair', async () => {
  const session = 'h13-06'

  const wire1 = await inject(undefined, session, [userMsg('u1')], 'guideline')
  const call1 = stableCallId(session, 1n)
  assert.equal(wire1[0].parts[0].callID, call1)
  assert.equal(wire1[0].parts[0].state.output, 'guideline')

  const wire2 = await inject(
    undefined,
    session,
    [userMsg('u1'), assistantText('a1'), userMsg('u2')],
    'tip2\n\nguideline',
  )
  const call2 = stableCallId(session, 2n)
  assert.equal(wire2[0].parts[0].state.output, 'guideline', 'pair1 marker bytes must never change')
  assert.equal(wire2[3].parts[0].state.output, 'tip2\n\nguideline')
  assert.equal(wire2[3].parts[0].callID, call2)
  assert.notEqual(call1, call2)

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

test('H13_08_n_round_property_prefix_law_holds', async () => {
  const rand = mulberry32(0x1357)
  const session = 'h13-08'
  const rounds = 8

  // history grows exactly as the Host transcript does: previous wire (synthetic
  // messages included) + the new real content of this round.
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

    // One round can create at most one new pair; a round whose terminal shape
    // repeats an existing placement (HOST-013 §8 dedupe) creates none.
    const pairCount = pairMessages(wire).length
    assert.ok(pairCount >= previousPairCount, `round ${n}: pair count must never shrink`)
    assert.ok(pairCount <= previousPairCount + 1, `round ${n}: at most one new pair per round`)

    if (previousWire !== undefined) {
      assertPrefixLaw(previousWire, wire, `H13-08 round ${n}`)
    }
    history = wire
    previousWire = wire
    previousPairCount = pairCount
  }
})
