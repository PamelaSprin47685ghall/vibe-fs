// tests/unit/temporal/fallback-aabb-confluence.test.mjs — G4R-1 Temporal Kernel theorem.
//
// Proves race-as-algebra on production FallbackProjection / Fold.
// No test-only business logic: every assertion folds through production code.
//
// Race model (G4R §10/§24):
//   Time is input, not authority. If A and B are independent (different
//   sessions / logical runs), fold(A;B) == fold(B;A) — confluence.
//   If they contend on one cursor, the journal has a unique precedence outcome
//   (exactly once, not double-counted). No scheduler lottery.
//
// Production symbols invoked:
//   - AgentPairCursor (recordFailure/success/side/isValidAdvance/recoveryVerdict)
//   - FallbackProjection (forAuthority/applyAdvance/applyExhausted/recordSuccess/mayContinue/read)
//   - Fold (fold.apply, fold.one, fold.session) + Envelope + Fact construction
//   - VirtualClock via harness (PtyTiming.createVirtualTimerPort)
//   - DeterministicEventQueue / DeterministicCompletionSource / dropEphemeral / runTrace
//   - agentJournal EventStore durable port (InMemoryGitRawStore + EventStoreJournalWriter)

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  authorityRoot,
  cursor,
  envelope,
  fact,
  fallbackProjection,
  fold,
  logicalRunId,
  providerRun,
  sessionId,
  stream,
  timerPort,
} from '../support/domain.mjs'
import {
  DeterministicCompletionSource,
  DeterministicEventQueue,
  DurableTraceEvents,
  createRecordedProviderPort,
  createVirtualClock,
  dropEphemeral,
  runTrace,
} from './harness.mjs'

// ── helpers ─────────────────────────────────────────────────────────────────

const SES_A = 'ses_a'
const SES_B = 'ses_b'
const RUN_L = logicalRunId('run_L')
const RUN_M = logicalRunId('run_M')
const ROOT_A = authorityRoot('msg_u1')
const ROOT_B = authorityRoot('msg_u2')
const SESSION_A = sessionId(SES_A)
const SESSION_B = sessionId(SES_B)

const identityFor = (session, logical, root, run) => cursor.attemptIdentity(session, logical, root, providerRun(run))

const rootAgentFact = (session, logical, root) =>
  agentFact('AuthorityRootAccepted', {
    SessionId: sessionId(session),
    LogicalRunId: logicalRunId(logical),
    AuthorityRootUserMessageId: authorityRoot(root),
    AuthorityKind: 'HumanRoot',
    SelectedAgent: 'fast-coder',
    PeerAgent: 'deep-coder',
    CanonicalRole: 'coder',
    SelectedTier: 'fast',
  })

const advanceAgentFact = (session, logical, root, run, previous, next, count) =>
  agentFact('FallbackCursorAdvanced', {
    SessionId: sessionId(session),
    LogicalRunId: logicalRunId(logical),
    AuthorityRootUserMessageId: authorityRoot(root),
    ProviderRun: providerRun(run),
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: 'provider_error',
  })

const rootFact = (session, logical, root) => fact('AuthorityRootAccepted', {
  SessionId: sessionId(session),
  LogicalRunId: logicalRunId(logical),
  AuthorityRootUserMessageId: authorityRoot(root),
  AuthorityKind: 'HumanRoot',
  SelectedAgent: 'fast-coder',
  PeerAgent: 'deep-coder',
  CanonicalRole: 'coder',
  SelectedTier: 'fast',
})

const advanceFact = (session, logical, root, run, previous, next, count) =>
  fact('FallbackCursorAdvanced', {
    SessionId: sessionId(session),
    LogicalRunId: logicalRunId(logical),
    AuthorityRootUserMessageId: authorityRoot(root),
    ProviderRun: providerRun(run),
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: 'provider_error',
  })

const fallbackOf = (projection, sessionIdStr) => {
  const sess = fold.session(projection, sessionIdStr)
  if (!sess) return undefined
  return fallbackProjection.read(sess.Fallback)
}

// ── VirtualClock is time as input ──────────────────────────────────────────

test('TEMPORAL_virtual_clock_time_is_input_not_authority', async () => {
  const vt = createVirtualClock()
  let fired = 0
  const handle = vt.port.delay(100)
  handle.delay().then(() => {
    fired += 1
  })
  assert.equal(fired, 0, 'must not fire before advance')
  vt.advance(99)
  await new Promise((r) => setImmediate(r))
  assert.equal(fired, 0, '99ms of 100ms deadline must not fire')
  vt.advance(1)
  await handle.delay()
  assert.equal(fired, 1, 'advance past deadline fires exactly once')
  vt.port.dispose()
})

test('TEMPORAL_virtual_clock_cancel_and_dispose_yield_zero_callbacks', async () => {
  const vt = createVirtualClock()
  let fired = 0
  const a = vt.port.delay(10)
  const b = vt.port.delay(20)
  a.delay().then(() => {
    fired += 1
  })
  b.delay().then(() => {
    fired += 1
  })
  a.cancel()
  vt.advance(30)
  await new Promise((r) => setImmediate(r))
  assert.equal(fired, 1, 'cancelled handle must not fire; other handle fires once')
  vt.port.dispose()
  const c = vt.port.delay(10)
  let firedAfterDispose = 0
  // Dispose clears pending; new handles after dispose still enqueue but advance is no-op per PtyTiming.fs
  c.delay().then(() => {
    firedAfterDispose += 1
  })
  vt.advance(10)
  await new Promise((r) => setImmediate(r))
  // After port dispose, Advance is a no-op — so c must not fire.
  assert.equal(firedAfterDispose, 0)
})

// ── Deterministic queue enumerates races without waiting ────────────────────

test('TEMPORAL_deterministic_queue_enumerates_races_explicitly', () => {
  const a = ['A1', 'A2']
  const b = ['B1']
  const interleavings = DeterministicEventQueue.interleavings(a, b)
  // 3 choose 1 = 3 interleavings.
  assert.equal(interleavings.length, 3)
  const serialized = interleavings.map((xs) => xs.join(',')).sort()
  assert.deepEqual(serialized, ['A1,A2,B1', 'A1,B1,A2', 'B1,A1,A2'].sort())

  const perms = DeterministicEventQueue.permutations(['A', 'B', 'C'])
  assert.equal(perms.length, 6)
  // Every permutation contains same elements.
  for (const p of perms) assert.deepEqual([...p].sort(), ['A', 'B', 'C'])
})

// ── DeterministicCompletionSource resolves in explicit order ────────────────

test('TEMPORAL_completion_source_order_is_explicit', async () => {
  const src = new DeterministicCompletionSource()
  const e1 = src.enqueue()
  const e2 = src.enqueue()
  assert.equal(src.pendingCount, 2)
  // Resolve out of order by id — proves order is algebra, not queue lottery.
  src.resolveId(e2.id, 'second')
  src.resolveId(e1.id, 'first')
  const [first, second] = await Promise.all([e1.promise, e2.promise])
  assert.equal(first, 'first')
  assert.equal(second, 'second')
  assert.equal(src.pendingCount, 0)
})

// ── Theorem 1: independent sessions commute (A;B == B;A) ───────────────────
//
// Two independent fallback domains (different SessionId / LogicalRun) are
// commutative: the fold over their envelopes yields the same per-session
// cursors regardless of interleaving. This is the algebraic shape of
// "owner failure vs blogger interruption" when they land on different runs —
// but proved here at pure Fold level without spawning Host.

test('THEOREM_fallback_independent_sessions_commute_pure_projection', () => {
  const a0 = fallbackProjection.forAuthority(RUN_L, ROOT_A)
  const b0 = fallbackProjection.forAuthority(RUN_M, ROOT_B)

  const a1 = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, 'run_1'), 0, 1, 1, a0)
  const b1 = fallbackProjection.applyAdvance(identityFor(SESSION_B, RUN_M, ROOT_B, 'run_9'), 0, 1, 1, b0)
  assert.equal(a1.ok, true, `A advance failed: ${a1.error}`)
  assert.equal(b1.ok, true, `B advance failed: ${b1.error}`)

  // Independent — applying A then B vs B then A would be identical if the
  // projection were global; at single-projection level they are disjoint, so we
  // prove the Fold-level global commutativity instead (next test). Here prove
  // that neither projection observes the other.
  assert.deepEqual(fallbackProjection.read(a1.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(fallbackProjection.read(b1.value), {
    logicalRun: 'run_M',
    authorityRoot: 'msg_u2',
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

test('THEOREM_fold_independent_sessions_confluent_across_interleavings', () => {
  // Four envelopes: root+advance for ses_a, root+advance for ses_b.
  // Any interleaving of the two per-session sequences must fold to the same
  // global projection (per-session cursors identical). We enumerate a subset
  // explicitly rather than racing wall clocks.
  const seqA = [
    envelope({ seq: 10, stream: stream.session(SESSION_A), fact: rootFact(SES_A, 'run_L', 'msg_u1') }),
    envelope({ seq: 11, stream: stream.session(SESSION_A), fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_1', 0, 1, 1) }),
  ]
  const seqB = [
    envelope({ seq: 20, stream: stream.session(SESSION_B), fact: rootFact(SES_B, 'run_M', 'msg_u2') }),
    envelope({ seq: 21, stream: stream.session(SESSION_B), fact: advanceFact(SES_B, 'run_M', 'msg_u2', 'run_9', 0, 1, 1) }),
  ]

  // Two representative interleavings: A before B, and B before A.
  // Full enumeration would be DeterministicEventQueue.interleavings(seqA, seqB).
  const interleaveAB = [...seqA, ...seqB]
  const interleaveBA = [...seqB, ...seqA]

  const foldAB = fold.apply(fold.empty, interleaveAB)
  const foldBA = fold.apply(fold.empty, interleaveBA)
  assert.equal(foldAB.ok, true, foldAB.ok ? '' : JSON.stringify(foldAB.error))
  assert.equal(foldBA.ok, true, foldBA.ok ? '' : JSON.stringify(foldBA.error))

  // Both worlds agree per session.
  assert.deepEqual(fallbackOf(foldAB.value, SES_A), fallbackOf(foldBA.value, SES_A))
  assert.deepEqual(fallbackOf(foldAB.value, SES_B), fallbackOf(foldBA.value, SES_B))
  assert.equal(fallbackOf(foldAB.value, SES_A).offset, 1)
  assert.equal(fallbackOf(foldBA.value, SES_B).offset, 1)

  // Exhaustively prove confluence over all interleavings, not just two.
  for (const interleaving of DeterministicEventQueue.interleavings(seqA, seqB)) {
    const folded = fold.apply(fold.empty, interleaving)
    assert.equal(folded.ok, true)
    assert.deepEqual(fallbackOf(folded.value, SES_A), fallbackOf(foldAB.value, SES_A))
    assert.deepEqual(fallbackOf(folded.value, SES_B), fallbackOf(foldAB.value, SES_B))
  }
})

// ── Theorem 2: exactly-once / dedupe — same ProviderRun observed twice ───────
//
// FALLBACK-003: one logical failure observed by both a retry signal and an
// idle reconcile must advance the cursor once. The dedupe key is
// ProviderRunIdentity. Proving it as algebra: any trace that contains the same
// ProviderRun twice converges to count==1, regardless of interleaving with an
// unrelated independent event.

test('THEOREM_fallback_exactly_once_same_provider_run_advances_once', () => {
  const start = fallbackProjection.forAuthority(RUN_L, ROOT_A)
  const run = 'run_dup'

  // First observation valid 0→1.
  const first = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 0, 1, 1, start)
  assert.equal(first.ok, true)
  // Second observation of SAME run, now claiming 1→2, must be AlreadyObserved — not a second unit of budget.
  const second = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, first.value)
  assert.deepEqual(second, { ok: false, error: 'AlreadyObserved' })
  // State unchanged.
  assert.deepEqual(fallbackProjection.read(first.value), fallbackProjection.read(first.value))
  assert.equal(fallbackProjection.read(first.value).failures, 1)

  // Reverse-race algebra: if the duplicate's offset is stale, the valid step still wins.
  // Apply duplicate with stale offset first (1→2 from state 0): not deduped yet, so InvalidTransition.
  const staleFirst = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, start)
  assert.deepEqual(staleFirst, { ok: false, error: 'InvalidTransition' })
  // Then the valid 0→1 succeeds.
  const validAfterStale = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 0, 1, 1, start)
  assert.equal(validAfterStale.ok, true)
  // And any later duplicate is absorbed.
  const absorbed = fallbackProjection.applyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, validAfterStale.value)
  assert.deepEqual(absorbed, { ok: false, error: 'AlreadyObserved' })
  assert.deepEqual(fallbackProjection.read(validAfterStale.value), fallbackProjection.read(first.value))
})

test('THEOREM_fold_duplicate_absorbed_not_double_counted', () => {
  // At Fold level, a duplicate FallbackCursorAdvanced line is absorbed (fold stays ok, projection unchanged).
  // Two distinct traces that both contain the same ProviderRun twice must converge to failures==1.
  const root = envelope({ seq: 1, stream: stream.session(SESSION_A), fact: rootFact(SES_A, 'run_L', 'msg_u1') })
  const adv1 = envelope({
    seq: 2,
    stream: stream.session(SESSION_A),
    fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_dup', 0, 1, 1),
  })
  const dup = envelope({
    seq: 3,
    stream: stream.session(SESSION_A),
    fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_dup', 1, 2, 2),
  })

  const foldOnce = fold.apply(fold.empty, [root, adv1])
  const foldDup = fold.apply(fold.empty, [root, adv1, dup])
  assert.equal(foldOnce.ok, true)
  assert.equal(foldDup.ok, true, foldDup.ok ? '' : JSON.stringify(foldDup.error))
  assert.deepEqual(fallbackOf(foldOnce.value, SES_A), fallbackOf(foldDup.value, SES_A))
  assert.equal(fallbackOf(foldDup.value, SES_A).failures, 1)
  assert.equal(fallbackOf(foldDup.value, SES_A).dedupeKeys, 1)
})

// ── Theorem 3: precedence — contending advances on one cursor have a unique outcome
//
// Within one Logical Run, two valid-looking advances that both claim 0→1 contend.
// Exactly one wins; the other is InvalidTransition. The final offset/count is
// deterministic (1 failure) regardless of which contender won.

test('THEOREM_fallback_precedence_one_winner_for_one_cursor', () => {
  const start = fallbackProjection.forAuthority(RUN_L, ROOT_A)
  const attemptA = identityFor(SESSION_A, RUN_L, ROOT_A, 'run_a')
  const attemptB = identityFor(SESSION_A, RUN_L, ROOT_A, 'run_b')

  // Order A;B: A wins, B is stale.
  let s = fallbackProjection.applyAdvance(attemptA, 0, 1, 1, start)
  assert.equal(s.ok, true)
  const afterB = fallbackProjection.applyAdvance(attemptB, 0, 1, 1, s.value)
  assert.deepEqual(afterB, { ok: false, error: 'InvalidTransition' })
  const finalAB = fallbackProjection.read(s.value)

  // Order B;A: B wins, A is stale. Same offset/count.
  let t = fallbackProjection.applyAdvance(attemptB, 0, 1, 1, start)
  assert.equal(t.ok, true)
  const afterA = fallbackProjection.applyAdvance(attemptA, 0, 1, 1, t.value)
  assert.deepEqual(afterA, { ok: false, error: 'InvalidTransition' })
  const finalBA = fallbackProjection.read(t.value)

  assert.equal(finalAB.offset, 1)
  assert.equal(finalBA.offset, 1)
  assert.equal(finalAB.failures, 1)
  assert.equal(finalBA.failures, 1)
  // The cursor (offset/failures) converges; only the dedupe key identity differs (which run was remembered).
})

// ── Theorem 4: dropEphemeral preserves durable fallback facts ───────────────
//
// G4R §12: world1 → durable facts F, DROP EPHEMERAL, world2 := recover(F) → same cursor, no resurrection.

test('THEOREM_drop_ephemeral_preserves_fallback_cursor', async () => {
  const dir = `temporal-fallback-${Date.now()}-${Math.random().toString(16).slice(2)}`

  // Pure algebra: envelopes prove confluence/dedupe at Fold level.
  const seqEnvelopes = [
    envelope({ seq: 1, stream: stream.session(SESSION_A), fact: rootFact(SES_A, 'run_L', 'msg_u1') }),
    envelope({ seq: 2, stream: stream.session(SESSION_A), fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_1', 0, 1, 1) }),
    envelope({ seq: 3, stream: stream.session(SESSION_A), fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_2', 1, 2, 2) }),
  ]
  const beforeFold = fold.apply(fold.empty, seqEnvelopes)
  assert.equal(beforeFold.ok, true, beforeFold.ok ? '' : JSON.stringify(beforeFold.error))
  const beforeFallback = fallbackProjection.read(fold.session(beforeFold.value, SES_A).Fallback)
  assert.deepEqual({ offset: beforeFallback.offset, failures: beforeFallback.failures }, { offset: 2, failures: 2 })

  // Durable survival: write the SAME facts through AgentJournal's single writer (inner AgentFact),
  // then crash (dropEphemeral) and prove the recovered projection equals the pure one.
  const vt1 = createVirtualClock()
  const created1 = await agentJournal.create({ directory: dir, runtime: 'rt_1', pid: 4242, startedAt: '2026-01-01T00:00:00Z' })
  assert.equal(created1.ok, true, created1.ok ? '' : String(created1.error))
  const world1b = { vt: vt1, journal: created1.journal, raw: created1.raw, directory: dir, dispose: created1.dispose }
  const streamA = stream.session(SESSION_A)

  const agentFacts = [
    rootAgentFact(SES_A, 'run_L', 'msg_u1'),
    advanceAgentFact(SES_A, 'run_L', 'msg_u1', 'run_1', 0, 1, 1),
    advanceAgentFact(SES_A, 'run_L', 'msg_u1', 'run_2', 1, 2, 2),
  ]
  for (const af of agentFacts) {
    const appended = await agentJournal.appendAgent(streamA, undefined, af, world1b.journal)
    assert.equal(appended.ok, true, appended.ok ? '' : JSON.stringify(appended.error))
  }

  const snapBefore = agentJournal.snapshot(world1b.journal)
  const snapBeforeFallback = fallbackProjection.read(fold.session(snapBefore, SES_A).Fallback)
  assert.deepEqual(snapBeforeFallback, beforeFallback, 'durable snapshot must match pure fold')

  // Crash: drop ephemeral, recover durable via same EventStore directory.
  const world2 = await dropEphemeral(world1b, { runtime: 'rt_recovered', pid: 4243 })
  const after = agentJournal.snapshot(world2.journal)
  const afterFallback = fallbackProjection.read(fold.session(after, SES_A).Fallback)
  assert.deepEqual(afterFallback, beforeFallback, 'durable fallback cursor must survive dropEphemeral')

  // Replay of a duplicate via the durable fold still absorbs (no double-count).
  const persisted = await agentJournal.persistedEnvelopes(world2.journal)
  const replay = fold.apply(fold.empty, persisted)
  assert.equal(replay.ok, true, replay.ok ? '' : JSON.stringify(replay.error))
  const replayFallback = fallbackProjection.read(fold.session(replay.value, SES_A).Fallback)
  assert.deepEqual(replayFallback, beforeFallback, 'persisted envelope replay must converge')

  // Pure replay absorption.
  const rootDupEnvelope = envelope({ seq: 999, stream: streamA, fact: advanceFact(SES_A, 'run_L', 'msg_u1', 'run_1', 0, 1, 1) })
  const replayFold = fold.apply(beforeFold.value, [rootDupEnvelope])
  assert.equal(replayFold.ok, true, replayFold.ok ? '' : JSON.stringify(replayFold.error))
  const afterReplayFallback = fallbackProjection.read(fold.session(replayFold.value, SES_A).Fallback)
  assert.equal(afterReplayFallback.failures, 2, 'replaying a duplicate must not double-count')

  world2.dispose()
})

// ── runTrace composes VirtualClock + durable port ───────────────────────────

test('TEMPORAL_runTrace_advances_clock_and_appends_durably', async () => {
  const vt = createVirtualClock()
  const dir = `temporal-runtrace-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const created = await agentJournal.create({ directory: dir, runtime: 'rt_trace', pid: 4242 })
  assert.equal(created.ok, true)
  const world = { vt, journal: created.journal, raw: created.raw, directory: dir, dispose: created.dispose }

  let fired = 0
  const handle = vt.port.delay(50)
  handle.delay().then(() => {
    fired += 1
  })

  const streamA = stream.session(SESSION_A)
  const events = [
    DurableTraceEvents.appendAgentFact(streamA, undefined, rootAgentFact(SES_A, 'run_L', 'msg_u1')),
    DurableTraceEvents.advanceClock(30),
    DurableTraceEvents.appendAgentFact(streamA, providerRun('run_1'), advanceAgentFact(SES_A, 'run_L', 'msg_u1', 'run_1', 0, 1, 1)),
    DurableTraceEvents.advanceClock(20),
  ]
  await runTrace(world, events)
  await handle.delay()
  assert.equal(fired, 1, '50ms timer must fire after two advances totalling 50ms')
  const snap = agentJournal.snapshot(world.journal)
  const f = fallbackOf(snap, SES_A)
  assert.ok(f, 'fallback must exist after runTrace appends')
  assert.equal(f.failures, 1)
  world.dispose()
})

// ── RecordedProviderPort stub is deterministic ───────────────────────────────

test('TEMPORAL_recorded_provider_port_replays_in_enqueued_order', async () => {
  const port = createRecordedProviderPort()
  port.enqueue({ text: 'first' })
  port.enqueue({ text: 'second' })
  assert.equal(port.pendingCount, 2)
  const first = await port.request({})
  const second = await port.request({})
  assert.deepEqual(first, { text: 'first' })
  assert.deepEqual(second, { text: 'second' })
  assert.equal(port.pendingCount, 0)
})
