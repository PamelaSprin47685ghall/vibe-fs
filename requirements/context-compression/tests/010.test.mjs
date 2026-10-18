{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const selection = prefix
const agreeing = (digest) => () => digest
const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

test('WHAT[context-compression-010] CTX_011_no_completed_turn_yet_means_no_candidate', () => {
  // The first-turn state, and the post-reanchor state — one reason, because they are
  // the same situation: no cutoff claims anything.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 0,
    materialCutoff: 0,
    coveredDigest: '',
    requestStartCutoff: 3,
    recomputeDigest: agreeing(''),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoCoverage')
  assert.match(result.message, /no completed turn/)
})
test('WHAT[context-compression-010] CTX_011_a_retreating_candidate_is_refused', () => {
  const result = selection.select({
    committedEpoch: 2,
    committedSnapshot: committedAt(8),
    coverableCutoff: 3,
    materialCutoff: 3,
    coveredDigest: 'd3',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('d3'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'WouldRetreat')
  assert.match(result.message, /3 is behind the committed 8/)
})
test('WHAT[context-compression-010] CTX_011_an_identical_candidate_is_refused_before_an_epoch_is_spent', () => {
  // Same cutoff, same prefix digest, same FrozenRecordPrefix digest. Promoting it would spend an
  // epoch and a cold boundary on a prefix the model has already seen.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6' }),
    coverableCutoff: 6,
    materialCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('p6'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'NotNewerThanCommitted')
})
test('WHAT[context-compression-010] CTX_011_the_same_cutoff_with_a_tighter_B_is_a_new_candidate', () => {
  // The case a "cutoff must increase" rule would wrongly reject. A Y squash makes B
  // more compact without covering more X turns, and that IS worth a new epoch: the
  // model sees the same history in fewer tokens.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6-wide' }),
    coverableCutoff: 6,
    materialCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6-squashed',
    recomputeDigest: agreeing('p6'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 6)
})
test('WHAT[context-compression-010] COMPANION_011_a_digest_mismatch_fails_closed', () => {
  // The Companion recorded a digest for cutoff 5, but X's prefix now hashes to
  // something else. The numbering moved — a Host compaction, a pruned message — and
  // building a FrozenRecordPrefix here would describe turns the prefix no longer has.
  const result = selection.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    materialCutoff: 5,
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
test('WHAT[context-compression-010] COMPANION_011_the_proof_runs_even_when_the_candidate_looks_identical', () => {
  // An identity match computed from a stale numbering is not evidence of anything, so
  // the proof comes first. If this ordering flipped, a session whose prefix had been
  // renumbered would report `NotNewerThanCommitted` — a benign-sounding reason —
  // instead of the fail-closed mismatch.
  const result = selection.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6' }),
    coverableCutoff: 6,
    materialCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('prefix-has-been-renumbered'),
  })

  assert.equal(result.error, 'CutoffProofFailed', 'the proof must precede the identity comparison')
})
test('WHAT[context-compression-010] CTX_011_a_candidate_the_selector_refuses_is_one_the_fold_would_also_refuse', () => {
  // The two layers must agree on identity, since neither can import the other. An
  // identical candidate is refused here as `NotNewerThanCommitted` and there as
  // `CandidateNotNew`; if the field sets diverged, one side would build what the other
  // rejects and probes would silently stop promoting.
  const committed = committedAt(6, { digest: 'p6', frozen: 'f6' })

  const refusedBySelector = selection.select({
    committedEpoch: 1,
    committedSnapshot: committed,
    coverableCutoff: 6,
    materialCutoff: 6,
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
}
