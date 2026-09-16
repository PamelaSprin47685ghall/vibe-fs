import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as quiescence from '../../../dist/Execution/Host/QuiescencePermitSurface.js'

const aborted = ['aborted:host abort']

test('WHAT[CRASH-001] CRASH_JOIN_abort_observation_never_becomes_completion', () => {
  assert.equal(child.resolve('active', 'missing', aborted, '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-001] CRASH_JOIN_durable_abandoned_is_terminal_abandonment', () => {
  assert.equal(child.resolve('abandoned', 'missing', [], '').result, 'RecoveredAbandoned')
})

test('WHAT[CRASH-001] CRASH_JOIN_parent_cancelled_abandons_missing_child', () => {
  assert.equal(child.resolve('active', 'missing', ['parent-cancelled'], '').result, 'RecoveredAbandoned')
})

test('WHAT[CRASH-001] CRASH_JOIN_active_child_is_recovered_active_not_incomplete', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})

test('WHAT[CRASH-001] CRASH_JOIN_restore_in_flight_remains_incomplete_without_permit', () => {
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-001] CRASH_JOIN_unreadable_snapshot_remains_incomplete', () => {
  assert.equal(child.resolve('active', 'unreadable', ['active'], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-001] CRASH_JOIN_terminal_proof_is_joinable_only_with_body', () => {
  assert.equal(child.provenTerminal('').ok, false)
})

test('WHAT[CRASH-001] CRASH_JOIN_return_requires_proof_before_commit', () => {
  assert.deepEqual(child.provenTerminal('{"status":"ok"}'), {
    ok: true,
    finality: 'Succeeded',
    body: '{"status":"ok"}',
  })
})

test('WHAT[CRASH-001] Q07_restart_gate_holds_no_permit', () => {
  const gate = quiescence.create()
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-001] Q08_restart_or_unknown_idle_cannot_mint_new_send_authority', () => {
  const gate = quiescence.create()
  assert.equal(quiescence.canSend(gate, 'ses-1'), false)
})
