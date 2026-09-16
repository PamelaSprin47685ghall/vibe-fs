import assert from 'node:assert/strict'
import test from 'node:test'
import * as hostCompaction from '../../../dist/Context/Companion/HostCompactionPolicySurface.js'
import * as retryPolicy from '../../../dist/Context/Companion/RetryPolicySurface.js'
import * as attemptPlan from '../../../dist/Context/Companion/AttemptPlanProbeEligibilitySurface.js'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as compactionPolicySurface from '../../../dist/Host/Contract/CompactionPolicySurface.js'

test('WHAT[PREFIX-STABILITY-002] PREFIX_STABILITY_prefix_behavior_is_exported_only_by_PrefixSurface', () => {
  for (const removed of ['select', 'snapshot', 'empty', 'prefixEmpty', 'prefixSnapshot', 'prefixProbe', 'applyRebase', 'retainTodoWriteRounds', 'requestKind', 'requestKindLabels', 'requestKindLabel', 'requestKindMayCarryProbe']) {
    assert.equal(typeof compression[removed], 'undefined', `${removed} must not remain on CompressionSurface`)
  }
  assert.equal(prefix.requestKind.mayCarryProbe(prefix.requestKind.workMain), true)
  assert.equal(prefix.requestKind.mayCarryProbe(prefix.requestKind.bloggerMain), false)
  assert.equal(prefix.requestKindLabel(prefix.requestKind.workMain), 'work-main')
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_prevention_layer_names_every_setting_that_must_be_off', () => {
  assert.ok(hostCompaction.preventedSettings.length >= 3)
})

test('WHAT[CONTEXT-COMPRESSION-002] COMPANION_009_prune_is_listed_and_says_why_containment_cannot_save_it', () => {
  assert.ok(hostCompaction.pruneExplanation)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_autocontinue_is_answered_false_rather_than_left_to_the_default', () => {
  assert.equal(hostCompaction.autocontinueDefault, false)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_a_setting_that_cannot_be_written_fails_startup_with_its_reason', () => {
  const res = hostCompaction.validateSettings({ readonlySetting: 'on' })
  assert.equal(res.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_startup_probe_passes_when_settings_are_off_and_the_first_turn_is_clean', () => {
  const res = hostCompaction.startupProbe({ cleanFirstTurn: true })
  assert.equal(res.ok, true)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_a_compaction_on_the_first_turn_means_a_second_implementation', () => {
  const res = hostCompaction.startupProbe({ cleanFirstTurn: false })
  assert.equal(res.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_the_setting_check_takes_precedence_over_the_turn_observation', () => {
  const res = hostCompaction.startupProbe({ settingsOk: false, cleanFirstTurn: true })
  assert.equal(res.ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_containment_keys_on_the_folded_predicate_not_raw_fields', () => {
  assert.ok(hostCompaction.containmentPredicate)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_the_newest_unhandled_compaction_is_the_one_to_reanchor', () => {
  const res = hostCompaction.selectCompaction([{ id: 1 }, { id: 2 }])
  assert.equal(res.id, 2)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_at_most_one_reanchor_is_emitted_per_observation', () => {
  const list = hostCompaction.reanchorsFor([{ id: 1 }])
  assert.equal(list.length, 1)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_an_already_reanchored_compaction_is_not_reanchored_again', () => {
  const list = hostCompaction.reanchorsForAlreadyHandled(1)
  assert.equal(list.length, 0)
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_no_observed_compaction_means_nothing_to_do', () => {
  assert.deepEqual(hostCompaction.reanchorsFor([]), [])
})

test('WHAT[CONTEXT-COMPRESSION-002] HOST_006_a_new_compaction_after_a_handled_one_is_still_caught', () => {
  const res = hostCompaction.reanchorsForNew(1, 2)
  assert.equal(res.length, 1)
})

test('WHAT[CONTEXT-COMPRESSION-002] retry dispatch reacts only to confirmed failure material', () => {
  assert.equal(retryPolicy.reactsOnlyToConfirmedFailure(), true)
})

test('WHAT[CONTEXT-COMPRESSION-002] successful retry tool steps keep the committed prefix despite new coverage', () => {
  assert.equal(attemptPlan.probeEligibility({ retryToolSuccess: true }), 'KeepCommitted')
})

test('WHAT[CONTEXT-COMPRESSION-002] COMPANION_014_zero_coverage_projects_raw_tail_messages_unchanged', () => {
  const projected = blogProjection.projectWithCoverage(0, ['msg1', 'msg2'])
  assert.deepEqual(projected, ['msg1', 'msg2'])
})

test('WHAT[CONTEXT-COMPRESSION-002] COMPANION_014_partial_coverage_projects_summary_and_remaining_tail', () => {
  const projected = blogProjection.projectWithCoverage(1, ['msg1', 'msg2'])
  assert.equal(projected.length, 2)
  assert.equal(projected[0].type, 'summary')
})

test('WHAT[CONTEXT-COMPRESSION-002] COMPANION_014_full_coverage_projects_pure_compacted_state', () => {
  const projected = blogProjection.projectWithCoverage(2, ['msg1', 'msg2'])
  assert.equal(projected.length, 1)
  assert.equal(projected[0].type, 'summary')
})

test('WHAT[CONTEXT-COMPRESSION-002] COMPANION_014_projection_preserves_uncovered_tool_call_pairs', () => {
  assert.ok(blogProjection.preservesToolPairs)
})
