// Handle lifecycle crash matrix through the delegation-owned HandleSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const active = () => handles.crashScenario('active')

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_after_aborted_observed_stays_active', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.joinable, 0)
  assert.equal(state.retired, false)
})

test('WHAT[CRASH-005] P0_RECOVERY_JOIN_001_crash_before_handle_completed_append_has_no_completion', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.completion, null)
  assert.equal(state.joinable, 0)
})

test('WHAT[CRASH-002] P0_RECOVERY_JOIN_001_crash_after_completed_before_consume_is_awaiting_join', () => {
  const state = handles.crashScenario('completed')
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.deepEqual(state.completion, { kind: 'Terminal' })
  assert.equal(state.joinable, 1)
})

test('WHAT[CRASH-002] P0_RECOVERY_JOIN_001_duplicate_handle_completed_is_absorbed', () => {
  const state = handles.crashScenario('replayed-completed')
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.deepEqual(state.completion, { kind: 'Terminal' })
  assert.equal(state.joinable, 1)
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_001_crash_after_retired_is_idempotent', () => {
  const state = handles.crashScenario('retired')
  assert.equal(state.lifecycle, 'Retired')
  assert.equal(state.joinable, 0)
  assert.equal(state.retired, true)
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_001_duplicate_retire_and_late_complete_are_absorbed', () => {
  const state = handles.crashScenario('replayed-retired')
  assert.equal(state.lifecycle, 'Retired')
  assert.equal(state.retired, true)
  assert.equal(state.joinable, 0)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_matrix_no_aborted_durable_fact', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.completion, null)
  assert.equal(state.abandonReason, null)
})
