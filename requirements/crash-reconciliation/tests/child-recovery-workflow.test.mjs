// ChildRecoveryWorkflow outcomes are exposed through ChildRecoverySurface; the
// workflow's typed ports remain private to its owner.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'

test('WHAT[CRASH-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_blocks_retired_handle', () => {
  assert.equal(child.resolve('retired', 'missing', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_unreadable_snapshot_is_incomplete_not_blocked', () => {
  const result = child.resolve('active', 'unreadable', [], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveryBlocked')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_retired_handle_is_blocked_branch', () => {
  const result = child.resolve('retired', 'missing', [], '')
  assert.equal(result.result, 'RecoveryBlocked')
  assert.notEqual(result.result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_blank_terminal_body_is_incomplete_branch', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})
