import assert from 'node:assert/strict'
import test from 'node:test'
import * as restart from '../../../dist/Execution/Session/HostForkRestartLifecycleSurface.js'
import * as recovery from '../../../dist/Execution/Session/SessionRecoverySurface.js'

test('WHAT[MANAGED-SESSION-013] HFR_restart_abandoned_handle_recovered_abandoned', () => {
  assert.equal(restart.testRecoverAbandoned(), true)
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_retired_handle_recovered_retired', () => {
  assert.equal(restart.testRecoverRetired(), true)
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_host_owned_hidden_handle_is_filtered_out', () => {
  assert.equal(restart.testFilterHiddenHandles(), true)
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_active_handle_recovers_active', () => {
  assert.equal(restart.testRecoverActive(), true)
})

test('WHAT[MANAGED-SESSION-013] HFR_restart_recovery_commit_failure_blocks', () => {
  assert.equal(restart.testCommitFailureBlocks(), true)
})

test('WHAT[MANAGED-SESSION-013] session_recovery_contract_reenlist_filters_hidden_handles', () => {
  assert.equal(recovery.testReenlistFiltersHidden(), true)
})

test('WHAT[MANAGED-SESSION-013] session_recovery_contract_authorizes_family_without_physical_handle_leaks', () => {
  assert.equal(recovery.testAuthorizesFamilyNoLeaks(), true)
})
