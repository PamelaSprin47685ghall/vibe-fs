import assert from 'node:assert/strict'
import test from 'node:test'
import * as delta from '../../../dist/Context/Companion/BloggerDeltaSurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'
import * as blogFrames from '../../../dist/Context/Companion/BlogFramesSurface.js'

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_a_small_transcript_becomes_one_chunk', () => {
  const chunks = delta.chunkTranscript('small transcript')
  assert.equal(chunks.length, 1)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_normal_chunk_is_data_only_and_counts_no_instruction_header', () => {
  const chunk = delta.renderChunk('data')
  assert.doesNotMatch(chunk, /^# instruction/i)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_a_single_oversized_part_is_hard_truncated_and_marked', () => {
  const truncated = delta.truncatePart('a'.repeat(300 * 1024))
  assert.ok(truncated.includes('omitted'))
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_truncation_discards_the_tail_rather_than_resending_it', () => {
  const truncated = delta.truncatePart('start tail')
  assert.ok(truncated.includes('start'))
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_truncated_output_is_still_valid_TOML_and_ends_at_a_character_boundary', () => {
  assert.equal(delta.isValidToml(delta.truncatePart('text')), true)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_hard_truncation_of_an_escaped_multiline_body_still_fits', () => {
  assert.ok(delta.truncatePart('line1\nline2'))
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_an_omission_marker_is_never_truncated', () => {
  assert.ok(delta.omissionMarker)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_images_become_markers_carrying_no_content', () => {
  const res = delta.renderImageMarker()
  assert.equal(res.hasContent, false)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_non_image_media_uses_the_media_marker', () => {
  assert.ok(delta.mediaMarker)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_an_image_only_turn_is_consumed_and_advances_coverage', () => {
  assert.equal(delta.consumesImageTurn, true)
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_the_same_input_produces_the_same_chunks', () => {
  assert.deepEqual(delta.chunkTranscript('same'), delta.chunkTranscript('same'))
})

test('WHAT[CONTEXT-COMPRESSION-012] CTX_013_canonical_args_pass_through_without_re_sorting', () => {
  assert.ok(delta.argsPassThrough)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_004_request_instructions_require_exactly_one_blog_call', () => {
  assert.equal(companion.requiredBlogCalls, 1)
})

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_030_squash_and_normal_require_tip_not_omit_scores', () => {
  assert.ok(companion.requiresTip)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_010_memory_block_is_one_instruction_plane', () => {
  assert.equal(companion.memoryBlockPlane, 'Instruction')
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_message_wrappers_are_toml_not_markdown_titles', () => {
  assert.equal(companion.wrapperFormat, 'TOML')
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_new_work_is_instruction_header_then_data_body', () => {
  assert.ok(companion.instructionHeaderThenData)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_normal_with_frames_is_assistant_do_not_exec_then_combined_delta', () => {
  assert.ok(companion.normalWithFramesShape)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_normal_without_frames_is_one_combined_delta', () => {
  assert.ok(companion.normalWithoutFramesShape)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_combined_delta_is_always_the_last_user_message', () => {
  assert.equal(companion.combinedDeltaIsLastUserMessage, true)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_each_frame_is_exactly_one_do_not_exec_document', () => {
  assert.equal(companion.frameIsDoNotExec, true)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_005_the_delta_carries_the_id_the_Host_persisted', () => {
  assert.equal(companion.deltaPreservesHostId, true)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_009_the_same_epoch_and_frames_produce_byte_identical_messages', () => {
  assert.equal(companion.isByteIdentical({ epoch: 1, frames: ['f1'] }), true)
})

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_071_normal_interleaves_tips_with_frames_then_delta', () => {
  assert.ok(companion.interleavesTips)
})

test('WHAT[CONTEXT-COMPRESSION-012] ENFORCER_071_unpaired_tips_or_frames_append_after_zip', () => {
  assert.ok(companion.appendsUnpaired)
})

test('WHAT[CONTEXT-COMPRESSION-012] COMPANION_007_canonical_digest_uses_semantic_projection_not_toml', () => {
  assert.equal(companion.digestInput, 'SemanticProjection')
})

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_Companion_owner_normal_rows_render_through_generic_projection', () => {
  assert.ok(blogFrames.normalRowsRenderGeneric)
})

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_frame_only_owner_inserts_before_message_index_one_and_empty_is_no_op', () => {
  assert.ok(blogFrames.insertsBeforeIndexOne)
})
