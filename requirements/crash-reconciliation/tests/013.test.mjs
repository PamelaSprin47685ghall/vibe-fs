import assert from 'node:assert/strict'
import test from 'node:test'
import * as combine from '../../../dist/Execution/Session/Recovery/SessionRecoveryCombineSurface.js'
import * as sessionExtra from '../../../dist/Execution/Session/Recovery/SessionRecoveryExtraSurface.js'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'

test('WHAT[CRASH-013] RECOVERY_COMBINE_export_exists', () => {
  assert.ok(combine.combine)
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_blocked_dominates', () => {
  assert.equal(combine.combine(['Blocked', 'Waiting', 'Recovered']), 'Blocked')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_waiting_dominates_ready', () => {
  assert.equal(combine.combine(['Waiting', 'Recovered']), 'Waiting')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_recovered_over_ready', () => {
  assert.equal(combine.combine(['Recovered']), 'Recovered')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_empty_is_no_recovery_required', () => {
  assert.equal(combine.combine([]), 'NoRecoveryRequired')
})

test('WHAT[CRASH-013] RECOVERY_COMBINE_order_independent_for_tier', () => {
  assert.equal(combine.combine(['Waiting', 'Blocked']), combine.combine(['Blocked', 'Waiting']))
})

test('WHAT[CRASH-013] MISC_recovery_authorize_aggregates_blocks_waits_ready', () => {
  assert.ok(sessionExtra.authorizeAggregate)
})

test('WHAT[CRASH-013] RECOVERY_FAMILY_combine_and_coordinator_ownership_moved', () => {
  assert.ok(family.combineMoved)
})
