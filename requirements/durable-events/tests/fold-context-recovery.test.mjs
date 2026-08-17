// PERSIST-010 context recovery laws at the context-owned fold boundary.
// Inputs and projection summaries are plain data; the typed reducer remains
// behind ContextFoldSurface.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as contextFold from '../../../dist/Context/Companion/FoldSurface.js'

const SESSION = 'ses_ctx'
const BLOGGER = 'ses_ctx_blogger'
let sequence = 0

const next = (fact, run) => ({
  runtime: 'rt_ctx',
  seq: ++sequence,
  id: `event-${sequence}`,
  observedAt: '2026-04-01T08:00:00Z',
  session: SESSION,
  run,
  fact,
})

const entryFact = ({ epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `d-${cutoffTo}`, n = 1, run = `msg_e${n}` }) =>
  next(
    {
      family: 'Context',
      case: 'BlogObservationCommitted',
      payload: {
        SessionId: SESSION,
        BloggerSessionId: BLOGGER,
        RequestId: `req-e${n}`,
        FrameEpochId: epoch,
        PreviousIngestedThroughSequence: BigInt(from),
        NextIngestedThroughSequence: BigInt(to),
        PreviousCoverableTurnCutoffExclusive: cutoffFrom,
        NextCoverableTurnCutoffExclusive: cutoffTo,
        NextCoveredPrefixDigest: digest,
        TextRef: `blob-e${n}`,
        TextDigest: `sha-e${n}`,
        ProviderRun: run,
        ToolCallIds: [],
        TipRuleId: `enforcement-tip-${n}`,
        FieldNameAtCommit: `field-tip-${n}`,
        EvidenceRef: null,
        ObservedPrefixEpochId: 0,
      },
    },
    run,
  )

const squashFact = ({ previousEpoch, nextEpoch, count, n = 1, run = `msg_s${n}` }) =>
  next(
    {
      family: 'Context',
      case: 'BlogObservationsSquashed',
      payload: {
        SessionId: SESSION,
        BloggerSessionId: BLOGGER,
        RequestId: `req-s${n}`,
        PreviousFrameEpochId: previousEpoch,
        NextFrameEpochId: nextEpoch,
        CoveredFrameCount: count,
        TextRef: `blob-s${n}`,
        TextDigest: count === 1 ? 'sha-e1' : `sha-s${n}`,
        ProviderRun: run,
      },
    },
    run,
  )

const rebaseFact = ({ previousEpoch, nextEpoch, cutoff, seal = `seal-${cutoff}`, probe = `probe-${cutoff}`, run = `msg_p${cutoff}` }) =>
  next(
    {
      family: 'Context',
      case: 'PrefixRebaseCommitted',
      payload: {
        SessionId: SESSION,
        PreviousEpochId: previousEpoch,
        NextEpochId: nextEpoch,
        FrozenRecordPrefixRef: `blob-frozen-${cutoff}`,
        FrozenRecordPrefixDigest: `frozen-${cutoff}`,
        CutoffExclusive: cutoff,
        CoveredPrefixDigest: `prefix-${cutoff}`,
        SealRoot: seal,
        SyntheticMessageId: `synthetic-${seal}`,
        ProbeId: probe,
        SolvingProviderRun: run,
      },
    },
    run,
  )

const reanchorFact = ({ previousEpoch, nextEpoch, run = 'msg_compaction' }) =>
  next(
    {
      family: 'Context',
      case: 'ContextReanchored',
      payload: {
        SessionId: SESSION,
        PreviousEpochId: previousEpoch,
        NextEpochId: nextEpoch,
        ObservedCompactionRun: run,
      },
    },
    run,
  )

const foldOk = (envelopes) => {
  const result = contextFold.fold(envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value.sessions[SESSION]
}

const coverageOf = (session) => ({
  ingestedThroughSequence: Number(session.Blog.Coverage.IngestedThroughSequence),
  cutoff: session.Blog.Coverage.CoverableTurnCutoffExclusive,
  digest: session.Blog.Coverage.CoveredPrefixDigest,
})

// ── the happy path ──────────────────────────────────────────────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_entry_and_squash_fold_into_the_blog_projection', () => {
  const session = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
  ])

  assert.equal(Number(session.Blog.FrameEpochId), 1)
  assert.deepEqual(coverageOf(session), { ingestedThroughSequence: 2, cutoff: 2, digest: 'd-2' })
  assert.equal(session.PrefixEpoch == null, true)
})

test('WHAT[DURABLE-EVENTS-015] CTX_012_rebase_folds_into_the_prefix_projection_only', () => {
  const session = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1 }),
  ])

  assert.equal(Number(session.PrefixEpoch.EpochId), 1)
  assert.equal(session.PrefixEpoch.Snapshot.CutoffExclusive, 1)
  assert.deepEqual(coverageOf(session), { ingestedThroughSequence: 1, cutoff: 1, digest: 'd-1' })
  assert.equal(Number(session.Blog.FrameEpochId), 0)
})

// ── the asymmetry ──────────────────────────────────────────────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_a_stale_frame_epoch_fails_the_fold_closed', () => {
  const result = contextFold.fold([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
    entryFact({ epoch: 0, from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
  ])

  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'BlogObservationCommitted')
  assert.match(result.error.Reason, /frame epoch 1 is in force but the line was written against 0/)
  assert.match(result.error.Reason, /PERSIST-010/)
})

test('WHAT[DURABLE-EVENTS-015] CTX_012_a_replayed_rebase_is_absorbed_so_crash_recovery_is_idempotent', () => {
  const session = foldOk([
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }),
  ])

  assert.equal(Number(session.PrefixEpoch.EpochId), 1)
  assert.equal(session.PrefixEpoch.Snapshot.SealRoot, 'seal-P1')
})

test('WHAT[DURABLE-EVENTS-015] CTX_011_a_not_new_candidate_is_absorbed_by_the_fold', () => {
  const session = foldOk([
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 5 }),
    rebaseFact({ previousEpoch: 1, nextEpoch: 2, cutoff: 5 }),
  ])

  assert.equal(Number(session.PrefixEpoch.EpochId), 1)
})

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_a_non_sequential_prefix_epoch_fails_the_fold_closed', () => {
  const result = contextFold.fold([rebaseFact({ previousEpoch: 0, nextEpoch: 3, cutoff: 2 })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'PrefixRebaseCommitted')
  assert.match(result.error.Reason, /not the successor/)
})

test('WHAT[DURABLE-EVENTS-015] CTX_011_a_retreating_cutoff_fails_the_fold_closed', () => {
  const result = contextFold.fold([
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 8 }),
    rebaseFact({ previousEpoch: 1, nextEpoch: 2, cutoff: 3 }),
  ])

  assert.equal(result.ok, false)
  assert.match(result.error.Reason, /promoted cutoff 3 is earlier than the committed 8/)
  assert.match(result.error.Reason, /CTX-011/)
})

// ── reanchor: one fact, two projections, atomically ─────────────────────────

test('WHAT[DURABLE-EVENTS-015] HOST_006_reanchor_retires_the_prefix_and_zeroes_prefix_coverage_in_one_fact', () => {
  const before = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 2 }),
  ])

  assert.equal(before.PrefixEpoch.Snapshot.CutoffExclusive, 2)
  assert.deepEqual(coverageOf(before), { ingestedThroughSequence: 2, cutoff: 2, digest: 'd-2' })

  const after = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 2 }),
    reanchorFact({ previousEpoch: 1, nextEpoch: 2 }),
  ])

  assert.equal(after.PrefixEpoch.Snapshot == null, true)
  assert.equal(Number(after.PrefixEpoch.EpochId), 2)
  assert.deepEqual(coverageOf(after), { ingestedThroughSequence: 2, cutoff: 0, digest: '' })
  assert.deepEqual(after.Blog.FrameKinds, ['Entry', 'Entry'])
  assert.equal(after.Blog.FrameCount, before.Blog.FrameCount)
  assert.equal(Number(after.Blog.FrameEpochId), 0)
})

test('WHAT[DURABLE-EVENTS-015] HOST_006_a_replayed_reanchor_leaves_rebuilt_coverage_alone', () => {
  const session = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    reanchorFact({ previousEpoch: 0, nextEpoch: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 0, cutoffTo: 1, digest: 'rebuilt', n: 2 }),
    entryFact({ from: 2, to: 3, cutoffFrom: 1, cutoffTo: 2, digest: 'rebuilt-2', n: 3 }),
    reanchorFact({ previousEpoch: 0, nextEpoch: 1 }),
  ])

  assert.equal(Number(session.PrefixEpoch.EpochId), 1)
  assert.deepEqual(coverageOf(session), { ingestedThroughSequence: 3, cutoff: 2, digest: 'rebuilt-2' })
})

test('WHAT[DURABLE-EVENTS-015] HOST_006_coverage_and_probes_both_recover_after_a_reanchor', () => {
  const session = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1 }),
    reanchorFact({ previousEpoch: 1, nextEpoch: 2 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 0, cutoffTo: 1, digest: 'post', n: 2 }),
    rebaseFact({ previousEpoch: 2, nextEpoch: 3, cutoff: 1, seal: 'seal-after' }),
  ])

  assert.equal(Number(session.PrefixEpoch.EpochId), 3)
  assert.equal(session.PrefixEpoch.Snapshot.SealRoot, 'seal-after')
  assert.deepEqual(coverageOf(session), { ingestedThroughSequence: 2, cutoff: 1, digest: 'post' })
})

// ── persisted shape ─────────────────────────────────────────────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_context_recovery_facts_survive_NDJSON_and_still_fold', () => {
  const result = contextFold.replay([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1, seal: 'seal-rt' }),
    reanchorFact({ previousEpoch: 1, nextEpoch: 2 }),
  ])

  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const session = result.value.sessions[SESSION]
  assert.equal(Number(session.Blog.FrameEpochId), 1)
  assert.equal(Number(session.PrefixEpoch.EpochId), 2)
  assert.equal(session.PrefixEpoch.Snapshot == null, true)
  assert.deepEqual(coverageOf(session), { ingestedThroughSequence: 1, cutoff: 0, digest: '' })
})
