// tests/unit/Context/probe-selection.test.mjs — CTX-011 candidate selection.
//
// The nine steps that decide whether an armed slot gets a probe at all.
//
// Every refusal here is a NORMAL outcome, not an error: CTX-011 says an armed slot
// with no candidate sends its ordinary main request. So the tests assert which reason
// fired, not that something failed — the caller treats all reasons alike, and the
// distinction exists only so a diagnostic can say what happened.
//
// One check is load-bearing above the rest. Steps 1–2 compare numbers the plugin
// itself recorded; step 5 compares the Companion's recorded digest against a fresh
// hash of X's CURRENT prefix. That is the only check that can notice the numbering
// moved underneath — a Host compaction, a pruned message — and without it the probe
// would build a FrozenRecordPrefix describing turns the prefix no longer has.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as selection from '../../../dist/Context/Companion/CompressionSurface.js'
const prefix = selection

/** A digest oracle that agrees with the Companion at every cutoff. */
const agreeing = (digest) => () => digest

/** The committed snapshot, built through the projection's own constructor. */
const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

// ── step 1: nothing to build from ──────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_no_completed_turn_yet_means_no_candidate', () => {
  // The first-turn state, and the post-reanchor state — one reason, because they are
  // the same situation: no cutoff claims anything.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 0,
    coveredDigest: '',
    requestStartCutoff: 3,
    recomputeDigest: agreeing(''),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoCoverage')
  assert.match(result.message, /no completed turn/)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_coverage_inside_the_live_tail_means_no_candidate', () => {
  // `requestStartCutoff = 0` is the first request of a session: there are no turns
  // before the message being answered, so any candidate would have to swallow it.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'd5',
    requestStartCutoff: 0,
    recomputeDigest: agreeing('d5'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'CoverageNotAheadOfRequest')
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_candidate_never_swallows_the_message_being_answered', () => {
  // Companion is ahead of the request boundary — it consumed turns this request has
  // not sent yet. The candidate must clamp to the request's own start, or the probe
  // would replace the user message the model is supposed to answer.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 9,
    coveredDigest: 'd-clamped',
    requestStartCutoff: 4,
    recomputeDigest: agreeing('d-clamped'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 4, 'clamped to the request start, not the Companion coverage')
})

// ── step 2: the candidate must move things forward ─────────────────────────

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_a_retreating_candidate_is_refused', () => {
  const result = selection.select({
    committedEpoch: 2,
    committedSnapshot: committedAt(8),
    coverableCutoff: 3,
    coveredDigest: 'd3',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('d3'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'WouldRetreat')
  assert.match(result.message, /3 is behind the committed 8/)
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_an_identical_candidate_is_refused_before_an_epoch_is_spent', () => {
  // Same cutoff, same prefix digest, same FrozenRecordPrefix digest. Promoting it would spend an
  // epoch and a cold boundary on a prefix the model has already seen.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6' }),
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('p6'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'NotNewerThanCommitted')
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_the_same_cutoff_with_a_tighter_B_is_a_new_candidate', () => {
  // The case a "cutoff must increase" rule would wrongly reject. A Y squash makes B
  // more compact without covering more X turns, and that IS worth a new epoch: the
  // model sees the same history in fewer tokens.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6-wide' }),
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6-squashed',
    recomputeDigest: agreeing('p6'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 6)
})

// ── step 5: the cutoff proof ───────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-010] COMPANION_011_a_digest_mismatch_fails_closed', () => {
  // The Companion recorded a digest for cutoff 5, but X's prefix now hashes to
  // something else. The numbering moved — a Host compaction, a pruned message — and
  // building a FrozenRecordPrefix here would describe turns the prefix no longer has.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'recorded-when-consumed',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('what-the-prefix-hashes-to-now'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'CutoffProofFailed')
  assert.match(result.message, /recorded-when-consumed/)
  assert.match(result.message, /what-the-prefix-hashes-to-now/)
  assert.match(result.message, /COMPANION-011/)
})

test('WHAT[CONTEXT-COMPRESSION-016] COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff', () => {
  // Step 1 clamps before step 5 hashes. Hashing the Companion's unclamped cutoff would
  // prove a prefix the candidate does not actually use — the check would pass while
  // describing a different range.
  const asked = []

  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 9,
    coveredDigest: 'd-at-4',
    requestStartCutoff: 4,
    recomputeDigest: (cutoff) => {
      asked.push(cutoff)
      return cutoff === 4 ? 'd-at-4' : 'wrong-range'
    },
  })

  assert.deepEqual(asked, [4], 'the proof must hash the clamped cutoff exactly once')
  assert.equal(result.ok, true, result.ok ? '' : result.message)
})

test('WHAT[CONTEXT-COMPRESSION-010] COMPANION_011_the_proof_runs_even_when_the_candidate_looks_identical', () => {
  // An identity match computed from a stale numbering is not evidence of anything, so
  // the proof comes first. If this ordering flipped, a session whose prefix had been
  // renumbered would report `NotNewerThanCommitted` — a benign-sounding reason —
  // instead of the fail-closed mismatch.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6' }),
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('prefix-has-been-renumbered'),
  })

  assert.equal(result.error, 'CutoffProofFailed', 'the proof must precede the identity comparison')
})

// ── steps 6–8: what a built probe carries ──────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_the_probe_carries_the_seal_the_promotion_will_reuse', () => {
  // COMPANION-013: the seal is derived from the candidate's identity plus the epoch it
  // was built from. That is what lets CTX-012 promote the snapshot verbatim — the seal
  // the successful request used is already the one the committed epoch needs, so
  // promotion adds no second cold boundary.
  const result = selection.select({
    session: 'ses_x',
    committedEpoch: 3,
    committedSnapshot: undefined,
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    frozenDigest: 'f7',
    recomputeDigest: agreeing('p7'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)

  // The visible sha256 stand-in shows exactly which fields went in.
  assert.equal(result.sealRoot, '«ses_x|3|7|p7|f7»')

  // Both derived ids hash the ALREADY-HASHED seal, so the stand-in nests. That is the
  // real shape, not an artefact of the test double: COMPANION-013 derives them from the
  // seal rather than from its inputs, so two candidates that produce the same seal
  // necessarily produce the same message id — which is what keeps the provider from
  // seeing a changed prefix.
  assert.equal(result.syntheticId, '««ses_x|3|7|p7|f7»|companion-memory»')
  assert.equal(result.probeId, '««ses_x|3|7|p7|f7»|probe»')

  // And they are distinct from each other: one addresses a message, the other
  // identifies the attempt, and CTX-012's fold matches on the ProbeId alone.
  assert.notEqual(result.syntheticId, result.probeId)
})

test('WHAT[CONTEXT-COMPRESSION-009] CTX_010_the_probe_records_the_epoch_it_was_built_from', () => {
  // `BasedOnEpochId` is what the promotion is validated against: a probe built while
  // epoch 3 was in force may only promote to 4. A probe that raced a concurrent
  // reanchor is then refused rather than applied to the wrong base.
  const result = selection.select({
    committedEpoch: 3,
    committedSnapshot: committedAt(2),
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('p7'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.basedOnEpoch, 3n)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_the_built_candidate_is_exactly_what_the_projection_will_promote', () => {
  // End to end across the two modules: the snapshot the selector produced is accepted
  // by the fold unchanged. If either side reconstructed a field, this would be where
  // the drift showed.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'p5',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('p5'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)

  const promoted = prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: result.candidate }, prefix.empty)

  assert.equal(promoted.ok, true, promoted.ok ? '' : promoted.error)
  assert.deepEqual(promoted.value.snapshot, result.candidate, 'promoted byte-for-byte, not rebuilt')
  assert.equal(promoted.value.snapshot.sealRoot, result.sealRoot)
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_a_candidate_the_selector_refuses_is_one_the_fold_would_also_refuse', () => {
  // The two layers must agree on identity, since neither can import the other. An
  // identical candidate is refused here as `NotNewerThanCommitted` and there as
  // `CandidateNotNew`; if the field sets diverged, one side would build what the other
  // rejects and probes would silently stop promoting.
  const committed = committedAt(6, { digest: 'p6', frozen: 'f6' })

  const refusedBySelector = selection.select({
    committedEpoch: 1,
    committedSnapshot: committed,
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('p6'),
  })

  assert.equal(refusedBySelector.error, 'NotNewerThanCommitted')

  const state = prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: committed }, prefix.empty).value
  const refusedByFold = prefix.applyRebase({ previousEpoch: 1, nextEpoch: 2, candidate: committed }, state)

  assert.deepEqual(refusedByFold, { ok: false, error: 'CandidateNotNew' })
})
