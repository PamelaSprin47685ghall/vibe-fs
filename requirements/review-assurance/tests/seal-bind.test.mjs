// tests/unit/Review/seal-bindRun.test.mjs — HOST-010.
//
// bindableRun: the one assistant this transform is about to feed.
// All four conditions required; any miss fail-closed.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const msg = ({ id, role, parentID, completed, agent, summary, mode }) => ({
  id,
  role,
  parentID,
  completed: Boolean(completed),
  agent,
  summary,
  mode,
})

const bindRun = (physicalUser, messages) => review.bindableRun(physicalUser, messages)

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_positive_unique_incomplete_assistant_with_matching_parent', () => {
  const physical = 'msg_user_1'
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical }),
  ])
  assert.deepEqual(outcome, {
    ok: true,
    id: 'msg_asst_1',
    parentId: physical,
    completed: false,
  })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_parent_id_mismatch_is_no_bindRunable_run', () => {
  const outcome = bindRun('msg_user_1', [
    msg({ id: 'msg_user_1', role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: 'msg_other_user' }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NoBindableRun' })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_completed_assistant_is_no_bindRunable_run', () => {
  const physical = 'msg_user_1'
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical, completed: true }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NoBindableRun' })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_non_assistant_never_bindRuns', () => {
  const physical = 'msg_user_1'
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_tool_1', role: 'tool', parentID: physical }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NoBindableRun' })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_ambiguous_run_when_two_incomplete_children', () => {
  const physical = 'msg_user_1'
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'msg_asst_2', role: 'assistant', parentID: physical }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'AmbiguousRun', count: 2 })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_compaction_assistant_is_no_bindRunable_run', () => {
  const physical = 'msg_user_1'
  // Host compaction path: agent/mode = compaction or summary = true.
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_comp_1', role: 'assistant', parentID: physical, agent: 'compaction' }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NoBindableRun' })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_not_latest_run_when_newer_assistant_exists', () => {
  const physical = 'msg_user_1'
  // Candidate matches parent but a newer assistant exists (different parent/completed).
  // max id among assistants is msg_asst_9; candidate is msg_asst_1 → NotLatestRun.
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'msg_asst_9', role: 'assistant', parentID: 'msg_older', completed: true }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NotLatestRun' })
})

test('WHAT[REVIEW-ASSURANCE-007] HOST_010_summary_true_is_compaction', () => {
  const physical = 'msg_user_1'
  const outcome = bindRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_sum_1', role: 'assistant', parentID: physical, summary: true }),
  ])
  assert.deepEqual(outcome, { ok: false, error: 'NoBindableRun' })
})
