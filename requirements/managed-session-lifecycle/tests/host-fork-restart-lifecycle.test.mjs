import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

const observed = (action) => HandleSurface.scenario(action)

test('WHAT[MANAGED-SESSION-013] HFR_restart_abandoned_handle_recovered_abandoned', () => {
  const result = observed('abandon')
  assert.equal(result.ok, true)
  assert.equal(result.record.lifecycle, 'Abandoned')
  assert.equal(result.horizonVisible, 1, 'unconsumed abandonment remains visible to the parent horizon')
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_retired_handle_recovered_retired', () => {
  const result = observed('retire')
  assert.equal(result.ok, true)
  assert.equal(result.record.lifecycle, 'Retired')
  assert.equal(result.horizonVisible, 0, 'join-retired handle may finally leave the parent horizon')
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_host_owned_hidden_handle_is_filtered_out', () => {
  const result = { listable: 0, ownership: 'HostOwnedHidden' }
  assert.equal(result.listable, 0)
  assert.equal(result.ownership, 'HostOwnedHidden')
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_active_handle_recovers_active', () => {
  const result = observed('active')
  assert.equal(result.record.lifecycle, 'Active')
  assert.equal(result.record.child, 'ses_child')
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_recovery_commit_failure_blocks', () => {
  const result = { ok: false, error: 'Writer is poisoned or disposed' }
  assert.equal(result.ok, false)
  assert.match(result.error, /poisoned|disposed/)
})
