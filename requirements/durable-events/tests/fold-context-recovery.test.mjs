// requirements/durable-events/tests/fold-context-recovery.test.mjs — PERSIST-010 at the fold.
// Moved from tests/unit/context/fold-context-recovery.test.mjs (cutover Wave 2a); owner: durable-events.
//
// The projections in blog-projection.test.mjs and prefix-epoch.test.mjs decide
// whether a line is APPLICABLE. Fold decides what a refusal MEANS: absorbed as a
// benign replay, or fatal to startup (PERSIST-004).
//
// That split is the whole subject of this file, because the same shape of refusal
// gets opposite treatment on the two projections:
//
//   stale FRAME epoch   → fatal. A squash replaced the sequence, so an entry
//                         carrying the old epoch describes frames that no longer
//                         exist. Applying it is wrong and skipping it loses an
//                         entry whose delta was already consumed.
//   stale PREFIX epoch  → absorbed. CTX-012's crash recovery deliberately
//                         re-attempts a rebase or reanchor after a restart, so a
//                         stale line means "already applied".
//
// Getting this backwards is silent in both directions: absorbing a stale frame
// epoch corrupts the frame sequence, and rejecting a stale prefix epoch makes
// every crash recovery a startup failure.

import assert from 'node:assert/strict'
import test from 'node:test'
import { blogProjection as blog, bloggerRequestId, envelope, fact, fold, sessionId, stream, providerRun, blobRef, blobDigest, frameEpochId, prefixEpochId, idValue } from '../../verification-system/tests/support/domain.mjs'

const SESSION = 'ses_ctx'
const session = sessionId(SESSION)
const blogger = sessionId('ses_ctx_blogger')

let seq = 0
const next = (factValue, run) =>
  envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const entryFact = ({ epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `d-${cutoffTo}`, n = 1, run = `msg_e${n}` }) =>
  next(
    fact('BlogObservationCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(`req-e${n}`),
      FrameEpochId: frameEpochId(epoch),
      PreviousIngestedThroughSequence: BigInt(from),
      NextIngestedThroughSequence: BigInt(to),
      PreviousCoverableTurnCutoffExclusive: cutoffFrom,
      NextCoverableTurnCutoffExclusive: cutoffTo,
      NextCoveredPrefixDigest: digest,
      TextRef: blobRef(`blob-e${n}`),
      TextDigest: blobDigest(`sha-e${n}`),
      ProviderRun: providerRun(run),
      ToolCallIds: [],
      TipRuleId: `enforcement-tip-${n}`,
      FieldNameAtCommit: `field-tip-${n}`,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
    run,
  )

const squashFact = ({ previousEpoch, nextEpoch, count, n = 1, run = `msg_s${n}` }) =>
  next(
    fact('BlogObservationsSquashed', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(`req-s${n}`),
      PreviousFrameEpochId: frameEpochId(previousEpoch),
      NextFrameEpochId: frameEpochId(nextEpoch),
      CoveredFrameCount: count,
      TextRef: blobRef(`blob-s${n}`),
      TextDigest: blobDigest(`sha-s${n}`),
      ProviderRun: providerRun(run),
    }),
    run,
  )

const rebaseFact = ({ previousEpoch, nextEpoch, cutoff, seal = `seal-${cutoff}`, probe = `probe-${cutoff}`, run = `msg_p${cutoff}` }) =>
  next(
    fact('PrefixRebaseCommitted', {
      SessionId: session,
      PreviousEpochId: prefixEpochId(previousEpoch),
      NextEpochId: prefixEpochId(nextEpoch),
      FrozenRecordPrefixRef: blobRef(`blob-frozen-${cutoff}`),
      FrozenRecordPrefixDigest: blobDigest(`frozen-${cutoff}`),
      CutoffExclusive: cutoff,
      CoveredPrefixDigest: `prefix-${cutoff}`,
      SealRoot: seal,
      SyntheticMessageId: `synthetic-${seal}`,
      ProbeId: probe,
      SolvingProviderRun: providerRun(run),
    }),
    run,
  )

const reanchorFact = ({ previousEpoch, nextEpoch, run = 'msg_compaction' }) =>
  next(
    fact('ContextReanchored', {
      SessionId: session,
      PreviousEpochId: prefixEpochId(previousEpoch),
      NextEpochId: prefixEpochId(nextEpoch),
      ObservedCompactionRun: providerRun(run),
    }),
    run,
  )

/** Fold a sequence and require success, returning the one session's projections. */
const foldOk = (envelopes) => {
  const result = fold.apply(fold.empty, envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, SESSION)
}

const coverageOf = (s) => ({
  ingestedThroughSequence: Number(s.Blog.Coverage.IngestedThroughSequence),
  cutoff: s.Blog.Coverage.CoverableTurnCutoffExclusive,
  digest: s.Blog.Coverage.CoveredPrefixDigest,
})

// ── the happy path exists and lands in the right projections ────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_entry_and_squash_fold_into_the_blog_projection', () => {
  const s = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
  ])

  assert.equal(idValue.frameEpoch(s.Blog.FrameEpochId), 1n)
  assert.deepEqual(coverageOf(s), { ingestedThroughSequence: 2, cutoff: 2, digest: 'd-2' })

  // The prefix projection is untouched by frame facts: a session may have frames
  // long before any probe is promoted.
  assert.equal(s.PrefixEpoch, undefined)
})

test('WHAT[DURABLE-EVENTS-015] CTX_012_rebase_folds_into_the_prefix_projection_only', () => {
  const s = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1 }),
  ])

  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 1n)
  assert.equal(s.PrefixEpoch.Snapshot.CutoffExclusive, 1)

  // A promotion changes what X sends, not what Y has covered.
  assert.deepEqual(coverageOf(s), { ingestedThroughSequence: 1, cutoff: 1, digest: 'd-1' })
  assert.equal(idValue.frameEpoch(s.Blog.FrameEpochId), 0n)
})

// ── the asymmetry ──────────────────────────────────────────────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_a_stale_frame_epoch_fails_the_fold_closed', () => {
  // A squash moved the frame sequence to epoch 1; an entry still carrying epoch 0
  // describes frames that no longer exist. PERSIST-004: refuse the journal.
  const result = fold.apply(fold.empty, [
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
    entryFact({ epoch: 0, from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, n: 2 }),
  ])

  assert.equal(result.ok, false, 'a stale frame epoch must not be absorbed')
  assert.equal(result.error.Fact, 'BlogObservationCommitted')
  assert.match(result.error.Reason, /frame epoch 1 is in force but the line was written against 0/)
  assert.match(result.error.Reason, /PERSIST-010/)
})

test('WHAT[DURABLE-EVENTS-015] CTX_012_a_replayed_rebase_is_absorbed_so_crash_recovery_is_idempotent', () => {
  // The recovery path in CTX-012 cannot know whether the append landed before the
  // crash, so it re-attempts. The replay carries an epoch the projection has left.
  const s = foldOk([
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }),
  ])

  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 1n, 'one promotion, not two')
  assert.equal(s.PrefixEpoch.Snapshot.SealRoot, 'seal-P1')
})

test('WHAT[DURABLE-EVENTS-015] CTX_011_a_not_new_candidate_is_absorbed_by_the_fold', () => {
  // CTX-011 refuses to build such a probe, so a line carrying one is a replay
  // under a different epoch number. Absorbing it keeps the epoch and its cold
  // boundary from being spent for an identical prefix.
  const s = foldOk([
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 5 }),
    rebaseFact({ previousEpoch: 1, nextEpoch: 2, cutoff: 5 }),
  ])

  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 1n)
})

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_a_non_sequential_prefix_epoch_fails_the_fold_closed', () => {
  // Unlike a stale epoch, a skipped one cannot come from a replay — no correct
  // writer produces it, so it is corruption.
  const result = fold.apply(fold.empty, [rebaseFact({ previousEpoch: 0, nextEpoch: 3, cutoff: 2 })])

  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'PrefixRebaseCommitted')
  assert.match(result.error.Reason, /not the successor/)
})

test('WHAT[DURABLE-EVENTS-015] CTX_011_a_retreating_cutoff_fails_the_fold_closed', () => {
  const result = fold.apply(fold.empty, [
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 8 }),
    rebaseFact({ previousEpoch: 1, nextEpoch: 2, cutoff: 3 }),
  ])

  assert.equal(result.ok, false)
  assert.match(result.error.Reason, /promoted cutoff 3 is earlier than the committed 8/)
  assert.match(result.error.Reason, /CTX-011/)
})

// ── reanchor: one fact, two projections, atomically ────────────────────────

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

  // Prefix half moved. A retired prefix beside a live Host cutoff claim in the
  // voided numbering is exactly the state one fact exists to prevent.
  // RecordCoverage stays: it is an XTrace cursor, not a Host index (COMPANION-008).
  assert.equal(after.PrefixEpoch.Snapshot, undefined, 'prefix retired')
  assert.equal(idValue.prefixEpoch(after.PrefixEpoch.EpochId), 2n)
  assert.deepEqual(
    coverageOf(after),
    { ingestedThroughSequence: 2, cutoff: 0, digest: '' },
    'prefix coverage zeroed; record coverage kept',
  )

  // Frames survive: the work really happened, only the Host index mapping was voided.
  assert.deepEqual(blog.frameKinds(after.Blog), ['Entry', 'Entry'], 'both entries survived the reanchor')
  assert.equal(blog.frameCount(after.Blog), blog.frameCount(before.Blog))
  assert.equal(idValue.frameEpoch(after.Blog.FrameEpochId), 0n, 'no frame changed, so the frame epoch stands')
})

test('WHAT[DURABLE-EVENTS-015] HOST_006_a_replayed_reanchor_leaves_rebuilt_coverage_alone', () => {
  // The dangerous case. Two observations of one compaction, with real work between
  // them: if the fold re-applied the second, it would wipe PrefixCoverage the session
  // legitimately rebuilt, and the next probe would silently never be built.
  // RecordCoverage continues across reanchor, so the next entry advances from 1.
  const s = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    reanchorFact({ previousEpoch: 0, nextEpoch: 1 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 0, cutoffTo: 1, digest: 'rebuilt', n: 2 }),
    entryFact({ from: 2, to: 3, cutoffFrom: 1, cutoffTo: 2, digest: 'rebuilt-2', n: 3 }),
    reanchorFact({ previousEpoch: 0, nextEpoch: 1 }),
  ])

  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 1n, 'one retirement, not two')
  assert.deepEqual(coverageOf(s), { ingestedThroughSequence: 3, cutoff: 2, digest: 'rebuilt-2' }, 'rebuilt coverage survived')
})

test('WHAT[DURABLE-EVENTS-015] HOST_006_coverage_and_probes_both_recover_after_a_reanchor', () => {
  // End to end: manual /compact, then normal work, then a probe promotes again.
  // This is the "best effort" promise in HOST-006 made concrete.
  const s = foldOk([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1 }),
    reanchorFact({ previousEpoch: 1, nextEpoch: 2 }),
    entryFact({ from: 1, to: 2, cutoffFrom: 0, cutoffTo: 1, digest: 'post', n: 2 }),
    rebaseFact({ previousEpoch: 2, nextEpoch: 3, cutoff: 1, seal: 'seal-after' }),
  ])

  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 3n)
  assert.equal(s.PrefixEpoch.Snapshot.SealRoot, 'seal-after')
  assert.deepEqual(coverageOf(s), { ingestedThroughSequence: 2, cutoff: 1, digest: 'post' })
})

// ── the persisted shape survives the round trip ────────────────────────────

test('WHAT[DURABLE-EVENTS-015] PERSIST_010_context_recovery_facts_survive_NDJSON_and_still_fold', () => {
  // The journal is the contract surface. A fact that folds in memory but loses a
  // typed field through serialisation would only fail after a restart.
  const result = fold.replay([
    entryFact({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 }),
    squashFact({ previousEpoch: 0, nextEpoch: 1, count: 1 }),
    rebaseFact({ previousEpoch: 0, nextEpoch: 1, cutoff: 1, seal: 'seal-rt' }),
    reanchorFact({ previousEpoch: 1, nextEpoch: 2 }),
  ])

  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const s = fold.session(result.value, SESSION)

  assert.equal(idValue.frameEpoch(s.Blog.FrameEpochId), 1n)
  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 2n)
  assert.equal(s.PrefixEpoch.Snapshot, undefined)
  // RecordCoverage kept at the pre-reanchor ingest; only PrefixCoverage is zeroed.
  assert.deepEqual(coverageOf(s), { ingestedThroughSequence: 1, cutoff: 0, digest: '' })
})
