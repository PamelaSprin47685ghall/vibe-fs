import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const AttachmentSurface = await import("../../../dist/Execution/Session/Attachment/AttachmentSurface.js");


test('WHAT[managed-session-lifecycle-001] EXEC_026_get_or_create_creates_and_binds_a_work_child_once', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'engineer', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.firstChild, 'child-1')
  assert.equal(observed.secondChild, 'child-1')
  assert.equal(observed.firstAgent, 'engineer')
})
test('WHAT[managed-session-lifecycle-001] EXEC_026_remove_and_remove_by_delegate_session_are_the_only_unbind_paths', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Coder', 'coder', 'coder', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.firstChild, observed.secondChild)
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


test('WHAT[managed-session-lifecycle-001] session_recovery_contract_attached_runtime_single_owner_pure_evidence', async () => {
  const result = await AttachmentSurface.scenario('owner_1', 'Engineer', 'engineer', 'engineer', true)
  assert.equal(result.created, 1)
  assert.equal(result.firstChild, 'child-1')
  assert.equal(result.secondChild, 'child-1')
  assert.equal(result.firstAgent, 'engineer')
})
}
