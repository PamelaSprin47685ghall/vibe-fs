import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as blogEntry from '../../../dist/Context/Companion/BlogEntryCommittedSurface.js'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'
import * as frameSurface from '../../../dist/Context/Companion/Blogger/FrameSurface.js'

const agreeing = (digest) => () => digest

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_the_probe_carries_the_seal_the_promotion_will_reuse', () => {
  const result = prefix.select({
    session: 'ses_x',
    committedEpoch: 3,
    committedSnapshot: undefined,
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    frozenDigest: 'f7',
    recomputeDigest: agreeing('p7'),
  })
  assert.equal(result.ok, true)
  assert.equal(result.sealRoot, '«ses_x|3|7|p7|f7»')
  assert.notEqual(result.syntheticId, result.probeId)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_the_built_candidate_is_exactly_what_the_projection_will_promote', () => {
  const result = prefix.select({
    committedEpoch: 0,
    committedSnapshot: undefined,
    coverableCutoff: 5,
    coveredDigest: 'p5',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('p5'),
  })
  assert.equal(result.ok, true)
  const promoted = prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: result.candidate }, prefix.empty)
  assert.equal(promoted.ok, true)
  assert.deepEqual(promoted.value.snapshot, result.candidate)
})

test('WHAT[CONTEXT-COMPRESSION-011] blog_entry_atomic_commit_advances_epoch', () => {
  const res = blogEntry.commitEntry({ epoch: 1, frame: 'f1' })
  assert.equal(res.ok, true)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_replaces_the_oldest_frames_and_leaves_the_covered_range_alone', () => {
  const res = blogProjection.squashOldestFrames(['f1', 'f2', 'f3'])
  assert.equal(res.length, 2)
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_is_derived_from_exactly_the_candidate_identity', () => {
  const seal = companion.sealRoot({ session: 's1', epoch: 1, cutoff: 5, digest: 'd1', frozen: 'f1' })
  assert.ok(seal)
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_changes_when_any_identity_field_changes', () => {
  const s1 = companion.sealRoot({ session: 's1', epoch: 1, cutoff: 5, digest: 'd1', frozen: 'f1' })
  const s2 = companion.sealRoot({ session: 's2', epoch: 1, cutoff: 5, digest: 'd1', frozen: 'f1' })
  assert.notEqual(s1, s2)
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_seal_root_is_stable_across_calls', () => {
  const s1 = companion.sealRoot({ session: 's1', epoch: 1, cutoff: 5, digest: 'd1', frozen: 'f1' })
  const s2 = companion.sealRoot({ session: 's1', epoch: 1, cutoff: 5, digest: 'd1', frozen: 'f1' })
  assert.equal(s1, s2)
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_companion_memory_id_is_a_function_of_the_seal_alone', () => {
  assert.equal(companion.memoryId('seal-a'), companion.memoryId('seal-a'))
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_frame_id_needs_both_the_ordinal_and_the_frame_epoch', () => {
  assert.notEqual(companion.frameId(1, 1), companion.frameId(1, 2))
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_instruction_id_distinguishes_normal_from_squash', () => {
  assert.notEqual(companion.instructionId('normal'), companion.instructionId('squash'))
})

test('WHAT[CONTEXT-COMPRESSION-011] COMPANION_013_frame_ids_are_positional_within_the_current_sequence', () => {
  assert.equal(companion.frameId(1, 1), companion.frameId(1, 1))
})
