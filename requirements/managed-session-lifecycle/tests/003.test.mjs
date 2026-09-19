import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SatelliteSurface = await import("../../../dist/OpenCode/Host/SatelliteSurface.js");


test('WHAT[managed-session-lifecycle-003] HOST_015_companion_reuses_exact_journal_linked_physical_child', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(true, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Reused')
  assert.equal(observed.child, 'blogger-1')
  assert.deepEqual(observed.created, [])
  assert.deepEqual(observed.linked, [['work', 'blogger-1', 'blogger']])
})
test('WHAT[managed-session-lifecycle-003] HOST_015_conflicting_restored_child_fails_closed', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(true, true, true, false)
  assert.equal(observed.ok, false)
  assert.match(observed.error, /Conflicting companion satellite recovery/)
  assert.deepEqual(observed.created, [])
  assert.deepEqual(observed.linked, [])
})
test('WHAT[managed-session-lifecycle-003] HOST_015_without_durable_link_never_adopts_matching_sibling', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(false, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Created')
  assert.equal(observed.child, 'created-1')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SessionsSurface = await import("../../../dist/OpenCode/Host/SessionsSurface.js");


test('WHAT[managed-session-lifecycle-003] HOST_015_abort_children_cascade_stays_keyed_on_family_root', () => {
  const parents = [{ child: 'child-1', parent: 'root' }, { child: 'child-2', parent: 'root' }]
  assert.deepEqual(SessionsSurface.physicalParents(parents, ['child-1', 'child-2']), ['root', 'root'])
  assert.equal(SessionsSurface.familyRoot(parents, 'child-1'), 'root')
  assert.equal(SessionsSurface.familyRoot(parents, 'child-2'), 'root')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const AttachmentSurface = await import("../../../dist/Execution/Session/Attachment/AttachmentSurface.js");
const RecoverySurface = await import("../../../dist/Execution/Session/Recovery/Surface.js");
const AssociationSurface = await import("../../../dist/Execution/Session/AssociationSurface.js");
const SatelliteSurface = await import("../../../dist/OpenCode/Host/SatelliteSurface.js");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");


test('WHAT[managed-session-lifecycle-003] session_recovery_contract_restart_reuses_matching_durable_association', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(true, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Reused')
  assert.deepEqual(observed.linked, [['work', 'blogger-1', 'blogger']])
})
test('WHAT[managed-session-lifecycle-003] session_recovery_contract_conflict_fails_closed_without_guessing', async () => {
  const conflict = await SatelliteSurface.SatelliteSurface_scenario(true, true, true, false)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /Conflicting companion satellite recovery/)

  const queryError = await SatelliteSurface.SatelliteSurface_scenario(false, false, false, true)
  assert.equal(queryError.ok, false)
  assert.match(queryError.error, /Cannot recover companion satellite/)
})
}
