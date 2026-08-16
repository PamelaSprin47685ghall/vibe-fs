// SessionRecovery priority algebra through the recovery owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[CRASH-013] RECOVERY_COMBINE_export_exists', () => {
  assert.equal(typeof recovery.combine, 'function')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_blocked_dominates', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Waiting', 'Blocked', 'Recovered']), 'Blocked')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_waiting_dominates_ready', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Recovered', 'Waiting']), 'Waiting')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_recovered_over_ready', () => {
  assert.equal(recovery.combine(['NoRecoveryRequired', 'Recovered']), 'Recovered')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_empty_is_no_recovery_required', () => {
  assert.equal(recovery.combine([]), 'NoRecoveryRequired')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_order_independent_for_tier', () => {
  assert.equal(recovery.combine(['Blocked', 'Waiting', 'Recovered']), 'Blocked')
  assert.equal(recovery.combine(['Recovered', 'Blocked', 'Waiting']), 'Blocked')
})
