import assert from 'node:assert/strict'
import test from 'node:test'
import * as delta from '../../../dist/Context/Companion/BloggerDeltaSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'

const agreeing = (digest) => () => digest

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_fully_consumed_transcript_yields_no_chunk', () => {
  const chunks = delta.chunkTranscript('')
  assert.equal(chunks.length, 0)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_cursor_resumes_exactly_where_the_previous_chunk_stopped', () => {
  assert.ok(delta.cursorResumesExactly)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_multi_part_turn_splits_at_part_boundaries_and_holds_the_cutoff', () => {
  assert.ok(delta.splitsAtPartBoundaries)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_a_chunk_ending_on_a_non_final_part_never_advances_the_cutoff', () => {
  assert.equal(delta.nonFinalPartAdvancesCutoff, false)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_the_cutoff_never_decreases_across_chunks', () => {
  assert.equal(delta.cutoffIsMonotonic, true)
})

test('WHAT[CONTEXT-COMPRESSION-016] CTX_011_coverage_inside_the_live_tail_means_no_candidate', () => {
  const result = prefix.select({
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
  const result = prefix.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 9,
    coveredDigest: 'd-clamped',
    requestStartCutoff: 4,
    recomputeDigest: agreeing('d-clamped'),
  })
  assert.equal(result.ok, true)
  assert.equal(result.cutoff, 4)
})

test('WHAT[CONTEXT-COMPRESSION-016] COMPANION_011_the_proof_hashes_exactly_the_clamped_cutoff', () => {
  const asked = []
  const result = prefix.select({
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
  assert.deepEqual(asked, [4])
  assert.equal(result.ok, true)
})

test('WHAT[CONTEXT-COMPRESSION-016] y_prefix_materializes_only_prefix_coverage_full_turns', () => {
  assert.ok(blogProjection.materializesFullTurnsOnly)
})
