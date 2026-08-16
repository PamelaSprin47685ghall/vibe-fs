// Fallback temporal theorem suite.
//
// Fallback facts, durable journal traces, and folds cross the production
// TemporalSurface as plain values. The owner keeps typed identities, unions,
// and projection state private while this suite proves race algebra.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as temporal from '../../../dist/Verification/TemporalSurface.js'
import {
  DeterministicEventQueue,
  createDurableWorld,
  dropEphemeral,
} from '../../verification-system/tests/support/temporal-harness.mjs'

// ── helpers ─────────────────────────────────────────────────────────────────

const SES_A = 'ses_a'
const SES_B = 'ses_b'
const RUN_L = 'run_L'
const RUN_M = 'run_M'
const ROOT_A = 'msg_u1'
const ROOT_B = 'msg_u2'
const SESSION_A = SES_A
const SESSION_B = SES_B

const stream = (session) => ({ kind: 'Session', session })

const identityFor = (session, logical, root, run) => ({
  session,
  logicalRun: logical,
  authorityRoot: root,
  providerRun: run,
})

const rootFact = (session, logical, root) => ({
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SessionId: session,
    LogicalRunId: logical,
    AuthorityRootUserMessageId: root,
    AuthorityKind: 'HumanRoot',
    SelectedAgent: 'fast-coder',
    PeerAgent: 'deep-coder',
    CanonicalRole: 'coder',
    SelectedTier: 'fast',
  },
})

const advanceFact = (session, logical, root, run, previous, next, count) => ({
  family: 'Fallback',
  case: 'FallbackCursorAdvanced',
  payload: {
    SessionId: session,
    LogicalRunId: logical,
    AuthorityRootUserMessageId: root,
    ProviderRun: run,
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: 'provider_error',
  },
})

const envelope = (seq, session, fact, run) => ({
  runtime: 'rt_temporal',
  seq,
  observedAt: '2026-01-01T00:00:00Z',
  id: `e${seq}`,
  stream: stream(session),
  ...(run === undefined ? {} : { run }),
  fact,
})

const fallbackOf = (projection, sessionId) => projection?.sessions?.[sessionId]?.fallback

const fold = (envelopes) => temporal.fold(envelopes)

// ── Theorem 1: independent sessions commute (A;B == B;A) ───────────────────

test('WHAT[PAR-015] THEOREM_fallback_independent_sessions_commute_pure_projection', () => {
  const a0 = temporal.fallbackForAuthority(RUN_L, ROOT_A)
  const b0 = temporal.fallbackForAuthority(RUN_M, ROOT_B)

  const a1 = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, 'run_1'), 0, 1, 1, a0)
  const b1 = temporal.fallbackApplyAdvance(identityFor(SESSION_B, RUN_M, ROOT_B, 'run_9'), 0, 1, 1, b0)
  assert.equal(a1.ok, true, `A advance failed: ${a1.error}`)
  assert.equal(b1.ok, true, `B advance failed: ${b1.error}`)

  assert.deepEqual(temporal.fallbackRead(a1.value), {
    logicalRun: RUN_L,
    authorityRoot: ROOT_A,
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(temporal.fallbackRead(b1.value), {
    logicalRun: RUN_M,
    authorityRoot: ROOT_B,
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

test('WHAT[PAR-001] THEOREM_fold_independent_sessions_confluent_across_interleavings', () => {
  const seqA = [
    envelope(10, SES_A, rootFact(SES_A, RUN_L, ROOT_A)),
    envelope(11, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_1', 0, 1, 1)),
  ]
  const seqB = [
    envelope(20, SES_B, rootFact(SES_B, RUN_M, ROOT_B)),
    envelope(21, SES_B, advanceFact(SES_B, RUN_M, ROOT_B, 'run_9', 0, 1, 1)),
  ]

  const foldAB = fold([...seqA, ...seqB])
  const foldBA = fold([...seqB, ...seqA])
  assert.equal(foldAB.ok, true, foldAB.ok ? '' : JSON.stringify(foldAB.error))
  assert.equal(foldBA.ok, true, foldBA.ok ? '' : JSON.stringify(foldBA.error))
  assert.deepEqual(fallbackOf(foldAB.value, SES_A), fallbackOf(foldBA.value, SES_A))
  assert.deepEqual(fallbackOf(foldAB.value, SES_B), fallbackOf(foldBA.value, SES_B))
  assert.equal(fallbackOf(foldAB.value, SES_A).offset, 1)
  assert.equal(fallbackOf(foldBA.value, SES_B).offset, 1)

  for (const interleaving of DeterministicEventQueue.interleavings(seqA, seqB)) {
    const folded = fold(interleaving)
    assert.equal(folded.ok, true)
    assert.deepEqual(fallbackOf(folded.value, SES_A), fallbackOf(foldAB.value, SES_A))
    assert.deepEqual(fallbackOf(folded.value, SES_B), fallbackOf(foldAB.value, SES_B))
  }
})

// ── Theorem 2: exactly-once / dedupe ─────────────────────────────────────────

test('WHAT[PAR-003] THEOREM_fallback_exactly_once_same_provider_run_advances_once', () => {
  const start = temporal.fallbackForAuthority(RUN_L, ROOT_A)
  const run = 'run_dup'

  const first = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 0, 1, 1, start)
  assert.equal(first.ok, true)
  const second = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, first.value)
  assert.deepEqual(second, { ok: false, error: 'AlreadyObserved' })
  assert.deepEqual(temporal.fallbackRead(first.value), temporal.fallbackRead(first.value))
  assert.equal(temporal.fallbackRead(first.value).failures, 1)

  const staleFirst = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, start)
  assert.deepEqual(staleFirst, { ok: false, error: 'InvalidTransition' })
  const validAfterStale = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 0, 1, 1, start)
  assert.equal(validAfterStale.ok, true)
  const absorbed = temporal.fallbackApplyAdvance(identityFor(SESSION_A, RUN_L, ROOT_A, run), 1, 2, 2, validAfterStale.value)
  assert.deepEqual(absorbed, { ok: false, error: 'AlreadyObserved' })
  assert.deepEqual(temporal.fallbackRead(validAfterStale.value), temporal.fallbackRead(first.value))
})

test('WHAT[PAR-003] THEOREM_fold_duplicate_absorbed_not_double_counted', () => {
  const root = envelope(1, SES_A, rootFact(SES_A, RUN_L, ROOT_A))
  const advance = envelope(2, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_dup', 0, 1, 1))
  const duplicate = envelope(3, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_dup', 1, 2, 2))

  const foldOnce = fold([root, advance])
  const foldDuplicate = fold([root, advance, duplicate])
  assert.equal(foldOnce.ok, true)
  assert.equal(foldDuplicate.ok, true, foldDuplicate.ok ? '' : JSON.stringify(foldDuplicate.error))
  assert.deepEqual(fallbackOf(foldOnce.value, SES_A), fallbackOf(foldDuplicate.value, SES_A))
  assert.equal(fallbackOf(foldDuplicate.value, SES_A).failures, 1)
  assert.equal(fallbackOf(foldDuplicate.value, SES_A).dedupeKeys, 1)
})

// ── Theorem 3: precedence ───────────────────────────────────────────────────

test('WHAT[PAR-003] THEOREM_fallback_precedence_one_winner_for_one_cursor', () => {
  const start = temporal.fallbackForAuthority(RUN_L, ROOT_A)
  const attemptA = identityFor(SESSION_A, RUN_L, ROOT_A, 'run_a')
  const attemptB = identityFor(SESSION_A, RUN_L, ROOT_A, 'run_b')

  const winnerAB = temporal.fallbackApplyAdvance(attemptA, 0, 1, 1, start)
  assert.equal(winnerAB.ok, true)
  const afterB = temporal.fallbackApplyAdvance(attemptB, 0, 1, 1, winnerAB.value)
  assert.deepEqual(afterB, { ok: false, error: 'InvalidTransition' })
  const finalAB = temporal.fallbackRead(winnerAB.value)

  const winnerBA = temporal.fallbackApplyAdvance(attemptB, 0, 1, 1, start)
  assert.equal(winnerBA.ok, true)
  const afterA = temporal.fallbackApplyAdvance(attemptA, 0, 1, 1, winnerBA.value)
  assert.deepEqual(afterA, { ok: false, error: 'InvalidTransition' })
  const finalBA = temporal.fallbackRead(winnerBA.value)

  assert.equal(finalAB.offset, 1)
  assert.equal(finalBA.offset, 1)
  assert.equal(finalAB.failures, 1)
  assert.equal(finalBA.failures, 1)
})

// ── Theorem 4: dropEphemeral preserves durable fallback facts ───────────────

test('WHAT[PAR-007] THEOREM_drop_ephemeral_preserves_fallback_cursor', async () => {
  const seqEnvelopes = [
    envelope(1, SES_A, rootFact(SES_A, RUN_L, ROOT_A)),
    envelope(2, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_1', 0, 1, 1)),
    envelope(3, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_2', 1, 2, 2)),
  ]
  const beforeFold = fold(seqEnvelopes)
  assert.equal(beforeFold.ok, true, beforeFold.ok ? '' : JSON.stringify(beforeFold.error))
  const beforeFallback = fallbackOf(beforeFold.value, SES_A)
  assert.deepEqual({ offset: beforeFallback.offset, failures: beforeFallback.failures }, { offset: 2, failures: 2 })

  const world1 = await createDurableWorld({ directory: 'temporal-fallback', runtime: 'rt_1', pid: 4242 })
  const streamA = stream(SESSION_A)
  const agentFacts = [
    rootFact(SES_A, RUN_L, ROOT_A),
    advanceFact(SES_A, RUN_L, ROOT_A, 'run_1', 0, 1, 1),
    advanceFact(SES_A, RUN_L, ROOT_A, 'run_2', 1, 2, 2),
  ]
  const runs = [undefined, 'run_1', 'run_2']
  for (let index = 0; index < agentFacts.length; index += 1) {
    const appended = await temporal.journalAppendAgent(world1.journal, streamA, runs[index], agentFacts[index])
    assert.equal(appended.ok, true, appended.ok ? '' : JSON.stringify(appended.error))
  }

  const snapshotBefore = temporal.journalSnapshot(world1.journal)
  const snapshotBeforeFallback = fallbackOf(snapshotBefore, SES_A)
  assert.deepEqual(snapshotBeforeFallback, beforeFallback, 'durable snapshot must match pure fold')

  const world2 = await dropEphemeral(world1, { runtime: 'rt_recovered', pid: 4243 })
  const afterFallback = fallbackOf(temporal.journalSnapshot(world2.journal), SES_A)
  assert.deepEqual(afterFallback, beforeFallback, 'durable fallback cursor must survive dropEphemeral')

  const persisted = temporal.journalPersistedEnvelopes(world2.journal)
  const replay = fold(persisted)
  assert.equal(replay.ok, true, replay.ok ? '' : JSON.stringify(replay.error))
  assert.deepEqual(fallbackOf(replay.value, SES_A), beforeFallback, 'persisted envelope replay must converge')

  const duplicate = envelope(999, SES_A, advanceFact(SES_A, RUN_L, ROOT_A, 'run_1', 0, 1, 1))
  const replayWithDuplicate = fold([...seqEnvelopes, duplicate])
  assert.equal(replayWithDuplicate.ok, true, replayWithDuplicate.ok ? '' : JSON.stringify(replayWithDuplicate.error))
  assert.equal(fallbackOf(replayWithDuplicate.value, SES_A).failures, 2, 'replaying a duplicate must not double-count')

  world2.dispose()
})
