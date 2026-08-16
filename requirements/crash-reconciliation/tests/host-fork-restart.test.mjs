// Restart recovery outcomes cross ChildRecovery/Handle owner surfaces. The
// Host restart implementation remains typed and opaque to this semantic zone.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

test('WHAT[CRASH-002] HFR_restart_empty_journal_yields_no_linked_handles', () => {
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-002] HFR_restart_completed_terminal_re_enlists_child_into_runtime', () => {
  assert.equal(child.resolve('completed', 'missing', [], 'work-record').result, 'RecoveredTerminal')
  assert.equal(handles.crashScenario('completed').lifecycle, 'CompletedAwaitingJoin')
})

test('WHAT[CRASH-002] HFR_restart_active_with_terminal_snapshot_recovered_terminal', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'work-record').result, 'RecoveredTerminal')
})

test('WHAT[CRASH-015] HFR_restart_multiple_children_recovered_in_link_order', () => {
  assert.deepEqual([
    child.resolve('active', 'active', ['active'], '').result,
    child.resolve('completed', 'missing', [], 'done').result,
    child.resolve('abandoned', 'missing', [], '').result,
  ], ['RecoveredActive', 'RecoveredTerminal', 'RecoveredAbandoned'])
})

test('WHAT[CRASH-015] HFR_restart_legacy_false_abort_waits_with_rejection_fact', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Restart.fs', import.meta.url), 'utf8')
  assert.match(source, /legacy false abort rejected/)
  assert.equal(child.resolve('active', 'missing', ['aborted:legacy'], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-015] HFR_restart_retired_legacy_false_abort_migrates_replacement_once', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Restart.fs', import.meta.url), 'utf8')
  assert.match(source, /tryMigrateRetiredFalseAbort/)
  assert.equal(handles.crashScenario('replayed-retired').retired, true)
})

test('WHAT[CRASH-015] HFR_restart_invalid_completion_blob_waits', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-005] HFR_restart_active_with_unreadable_snapshot_waits_for_terminal_evidence', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
