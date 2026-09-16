import assert from 'node:assert/strict'
import test from 'node:test'
import * as satellite from '../../../dist/Execution/Session/SatelliteRuntimeSurface.js'
import * as flattening from '../../../dist/Execution/Session/SessionFlatteningSurface.js'
import * as recovery from '../../../dist/Execution/Session/SessionRecoverySurface.js'

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_reuses_exact_journal_linked_physical_child', async () => {
  const r = await satellite.testCompanionReuse()
  assert.equal(r.reused, true)
})

test('WHAT[MANAGED-SESSION-003] HOST_015_conflicting_restored_child_fails_closed', async () => {
  const r = await satellite.testConflictingRestoredChild()
  assert.equal(r.ok, false)
})

test('WHAT[MANAGED-SESSION-003] HOST_015_without_durable_link_never_adopts_matching_sibling', async () => {
  const r = await satellite.testNoDurableLinkNoAdoption()
  assert.equal(r.adopted, false)
})

test('WHAT[MANAGED-SESSION-003] HOST_015_abort_children_cascade_stays_keyed_on_family_root', () => {
  const r = flattening.testCascadeKeyedOnRoot()
  assert.equal(r.keyedOnRoot, true)
})

test('WHAT[MANAGED-SESSION-003] session_recovery_contract_restart_reuses_matching_durable_association', async () => {
  const r = await recovery.testRestartReusesAssociation()
  assert.equal(r.reused, true)
})

test('WHAT[MANAGED-SESSION-003] session_recovery_contract_conflict_fails_closed_without_guessing', async () => {
  const r = await recovery.testConflictFailsClosed()
  assert.equal(r.ok, false)
})
