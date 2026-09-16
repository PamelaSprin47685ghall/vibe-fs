import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as runtimePermit from '../../../dist/Execution/Delegation/Fork/Host/HostForkRuntimePermitSurface.js'
import * as closurePermit from '../../../dist/Execution/Session/Recovery/RecoveryClosurePermitSurface.js'

test('WHAT[CRASH-011] VERIFY_008_bare_runtime_join_refusal_and_permit_validation', async () => {
  const res = await childWorkflow.validatePermit('ses_other', 0, 'ses_parent', 0, [], [])
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-011] HFRT_join_with_permit_root_mismatch_is_not_found', () => {
  const res = runtimePermit.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-011] HFRT_join_with_permit_stale_journal_sequence_is_not_found', () => {
  const res = runtimePermit.validatePermit('ses_hfrt', 1000, 'ses_hfrt', 0, [], [])
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-011] EXEC_023_permit_whose_recovered_member_is_gone_is_not_found', () => {
  const res = runtimePermit.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_vanished'], [])
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-011] EXEC_023_permit_survives_family_growth_after_recovery_closed', () => {
  const res = runtimePermit.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_hfrt'], ['W:ses_hfrt', 'C:ses_child>ses_grandchild'])
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-011] HFRT_join_with_valid_permit_passes_validation', () => {
  const res = runtimePermit.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, [], [])
  assert.deepEqual(res, { ok: true, error: 'NothingToJoin' })
})

test('WHAT[CRASH-011] HFRT_await_agent_with_permit_validation_error_maps_to_not_found', () => {
  const res = runtimePermit.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-011] CRASH_CLOSURE_permit_refuses_loss_and_admits_growth', () => {
  const res = closurePermit.validateGrowth(['a'], ['a', 'b'])
  assert.equal(res.ok, true)
  const loss = closurePermit.validateGrowth(['a', 'b'], ['a'])
  assert.equal(loss.ok, false)
})
