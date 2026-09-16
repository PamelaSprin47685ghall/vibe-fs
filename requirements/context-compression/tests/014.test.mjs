import assert from 'node:assert/strict'
import test from 'node:test'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'
import * as blogFrames from '../../../dist/Context/Companion/BlogFramesSurface.js'

test('WHAT[CONTEXT-COMPRESSION-014] COMPANION_006_squash_rewrites_first_half_of_frames_permanently', () => {
  assert.ok(blogProjection.squashRewritesFirstHalf)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_projects_only_oldest_historic_frames_then_instruction', () => {
  assert.ok(companion.squashProjectsOldestFrames)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_pairs_tips_with_covered_frames_then_instruction', () => {
  assert.ok(companion.squashPairsTips)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_ignores_a_delta_even_if_one_is_supplied', () => {
  assert.equal(companion.squashIgnoresDelta, true)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_never_shows_the_later_frames', () => {
  assert.equal(companion.squashNeverShowsLaterFrames, true)
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_and_normal_requests_use_different_last_message_ids', () => {
  assert.notEqual(companion.lastMessageId('squash'), companion.lastMessageId('normal'))
})

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_plan_has_zero_physical_messages_and_not_first_turn', () => {
  assert.ok(companion.squashPlanProperties)
})

test('WHAT[CONTEXT-COMPRESSION-014] PROJ_008_Companion_owner_squash_rows_render_through_generic_projection', () => {
  assert.ok(blogFrames.squashRowsRenderGeneric)
})
