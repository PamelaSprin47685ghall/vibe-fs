import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as quiescence from '../../../dist/Execution/Host/QuiescencePermitSurface.js'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'

test('WHAT[CRASH-006] VERIFY_008_provider_failure_admission_ordered_sequence', async () => {
  const res = await childWorkflow.failureAdmissionSequence()
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-006] VERIFY_008_workflow_main_session_failure_owner_proven_routing', async () => {
  const res = await childWorkflow.failureProvenRouting()
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-006] VERIFY_008_interaction_repair_no_direct_ledger_path_documented', () => {
  assert.doesNotMatch(childWorkflow.source(), /DirectLedgerBypass/)
})

test('WHAT[CRASH-006] Q01_normal_stable_idle_yields_one_consumable_permit', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), true)
})

test('WHAT[CRASH-006] Q02_new_provider_attempt_invalidates_the_old_permit', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.recordAttempt(gate, 'ses-1', 'att-2')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-006] Q03_repeated_idle_does_not_repeat_send', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.consume(gate, 'ses-1')
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  assert.equal(quiescence.canSend(gate, 'ses-1'), false)
})

test('WHAT[CRASH-006] Q04_new_attempt_own_idle_can_send_again', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.consume(gate, 'ses-1')
  quiescence.recordAttempt(gate, 'ses-1', 'att-2')
  quiescence.recordIdle(gate, 'ses-1', 'att-2')
  assert.equal(quiescence.canSend(gate, 'ses-1'), true)
})

test('WHAT[CRASH-006] Q04b_transport_idle_waits_for_all_active_tool_bodies', () => {
  const gate = quiescence.create()
  quiescence.startTool(gate, 'ses-1')
  quiescence.recordTransportIdle(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
  quiescence.endTool(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), true)
})

test('WHAT[CRASH-006] Q05_new_physical_user_material_revokes_the_previous_idle_before_transform', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.recordUserMaterial(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-006] Q05b_delayed_older_physical_replay_is_inert_after_newer_material', () => {
  const gate = quiescence.create()
  quiescence.recordUserMaterial(gate, 'ses-1')
  quiescence.recordOlderReplay(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-006] Q06_definitive_pre_acceptance_rejection_can_return_the_same_idle_permit', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.consume(gate, 'ses-1')
  quiescence.returnPermit(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), true)
})

test('WHAT[CRASH-006] Q10_session_deleted_drops_every_permit', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.deleteSession(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-006] P4_SURFACE_exports_exact_capability_names', () => {
  assert.ok(quiescence.exports)
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_constructor_does_not_start_fork_restore', () => {
  assert.doesNotMatch(family.constructorSource(), /startForkRestore/)
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_authorize_ready_issues_private_permit', () => {
  const res = family.authorize(['Recovered'])
  assert.equal(res.status, 'Ready')
  assert.ok(res.permit)
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_ready_before_business_is_type_enforced', () => {
  assert.ok(family.readyTypeEnforced)
})
