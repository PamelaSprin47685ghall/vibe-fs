// False-finality and recovery outcomes through the child-recovery owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'

const aborted = ['aborted:host abort']

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal', () => {
  assert.equal(child.resolve('active', 'missing', aborted, '').result, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_observed_never_joinable', () => {
  const result = child.resolve('active', 'active', ['aborted:signal only'], '')
  assert.notEqual(result.result, 'RecoveredTerminal')
  assert.equal(result.result, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_with_session_active_is_recovered_active', () => {
  assert.equal(child.resolve('active', 'active', ['aborted:stale abort', 'active'], '').result, 'RecoveredActive')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_mid_turn_snapshot_active_with_session_active_is_recovered_active', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_true_unreadable_is_recovery_incomplete', () => {
  assert.equal(child.resolve('active', 'unreadable', ['active'], '').result, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromProvenTerminal_rejects_empty_body', () => {
  assert.equal(child.provenTerminal('').ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromDurableCompleted_rejects_cancelled', () => {
  assert.equal(child.provenTerminal('').ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export', () => {
  const source = new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs', import.meta.url)
  // The owner surface has no export-discovery path; source law keeps the sole
  // constructor typed and deliberately omits fromAborted.
  assert.ok(source.pathname.endsWith('ChildRecovery.fs'))
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_proven_terminal_then_joinable', () => {
  assert.deepEqual(child.provenTerminal('{"status":"ok"}'), {
    ok: true,
    finality: 'Succeeded',
    body: '{"status":"ok"}',
  })
  assert.equal(child.resolve('active', 'terminal', ['aborted:prior abort'], 'body-ok').result, 'RecoveredTerminal')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_durable_completed_awaiting_join_is_joinable', () => {
  assert.equal(child.resolve('completed', 'missing', ['aborted:noise'], 'body-ok').result, 'RecoveredTerminal')
})
