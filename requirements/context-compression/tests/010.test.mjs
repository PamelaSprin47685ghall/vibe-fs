import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'

const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const agreeing = (digest) => () => digest

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_no_completed_turn_yet_means_no_candidate', () => {
  const result = prefix.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 0,
    coveredDigest: '',
    requestStartCutoff: 3,
    recomputeDigest: agreeing(''),
  })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoCoverage')
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_a_retreating_candidate_is_refused', () => {
  const result = prefix.select({
    committedEpoch: 2,
    committedSnapshot: committedAt(8),
    coverableCutoff: 3,
    coveredDigest: 'd3',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('d3'),
  })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'WouldRetreat')
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_an_identical_candidate_is_refused_before_an_epoch_is_spent', () => {
  const result = prefix.select({
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
  const result = prefix.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6-wide' }),
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6-squashed',
    recomputeDigest: agreeing('p6'),
  })
  assert.equal(result.ok, true)
  assert.equal(result.cutoff, 6)
})

test('WHAT[CONTEXT-COMPRESSION-010] COMPANION_011_a_digest_mismatch_fails_closed', () => {
  const result = prefix.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'recorded-when-consumed',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('what-the-prefix-hashes-to-now'),
  })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CutoffProofFailed')
})

test('WHAT[CONTEXT-COMPRESSION-010] COMPANION_011_the_proof_runs_even_when_the_candidate_looks_identical', () => {
  const result = prefix.select({
    committedEpoch: 1,
    committedSnapshot: committedAt(6, { digest: 'p6', frozen: 'f6' }),
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('prefix-has-been-renumbered'),
  })
  assert.equal(result.error, 'CutoffProofFailed')
})

test('WHAT[CONTEXT-COMPRESSION-010] CTX_011_a_candidate_the_selector_refuses_is_one_the_fold_would_also_refuse', () => {
  const committed = committedAt(6, { digest: 'p6', frozen: 'f6' })
  const refusedBySelector = prefix.select({
    committedEpoch: 1,
    committedSnapshot: committed,
    coverableCutoff: 6,
    coveredDigest: 'p6',
    requestStartCutoff: 20,
    frozenDigest: 'f6',
    recomputeDigest: agreeing('p6'),
  })
  assert.equal(refusedBySelector.error, 'NotNewerThanCommitted')
})

test('WHAT[CONTEXT-COMPRESSION-010] blog_projection_candidate_selection_strictly_newer', () => {
  assert.ok(blogProjection.candidateSelectionStrictlyNewer)
})
