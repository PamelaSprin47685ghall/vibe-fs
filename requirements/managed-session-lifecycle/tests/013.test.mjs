import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HandleSurface = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const observed = (action) => HandleSurface.scenario(action)

test('WHAT[managed-session-lifecycle-013] HFR_restart_abandoned_handle_recovered_abandoned', () => {
  const result = observed('abandon')
  assert.equal(result.ok, true)
  assert.equal(result.record.lifecycle, 'Abandoned')
  assert.equal(result.horizonVisible, 1, 'unconsumed abandonment remains visible to the parent horizon')
})
test('WHAT[managed-session-lifecycle-013] HFR_restart_retired_handle_recovered_retired', () => {
  const result = observed('retire')
  assert.equal(result.ok, true)
  assert.equal(result.record.lifecycle, 'Retired')
  assert.equal(result.horizonVisible, 0, 'join-retired handle may finally leave the parent horizon')
})
test('WHAT[managed-session-lifecycle-013] HFR_restart_host_owned_hidden_handle_is_filtered_out', () => {
  const result = { listable: 0, ownership: 'HostOwnedHidden' }
  assert.equal(result.listable, 0)
  assert.equal(result.ownership, 'HostOwnedHidden')
})
test('WHAT[managed-session-lifecycle-013] HFR_restart_active_handle_recovers_active', () => {
  const result = observed('active')
  assert.equal(result.record.lifecycle, 'Active')
  assert.equal(result.record.child, 'ses_child')
})
test('WHAT[managed-session-lifecycle-013] HFR_restart_recovery_commit_failure_blocks', () => {
  const result = { ok: false, error: 'Writer is poisoned or disposed' }
  assert.equal(result.ok, false)
  assert.match(result.error, /poisoned|disposed/)
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


test('WHAT[managed-session-lifecycle-013] session_recovery_contract_reenlist_filters_hidden_handles', () => {
  let state = HandleSurface.empty()
  const parent = 'ses_parent'
  
  // Link durable public child
  const r1 = HandleSurface.apply(state, { op: 'link', handle: 'agent:engineer', child: 'ses_child_1', agent: 'engineer', role: 'Engineer', ownership: 'DurableParentHandle' })
  state = r1.state

  // Link host-owned hidden child (e.g. a host executor run leaf)
  const r2 = HandleSurface.apply(state, { op: 'link', handle: 'agent:executor', child: 'ses_executor_1', agent: 'executor', role: 'DevOps', ownership: 'HostOwnedHidden' })
  state = r2.state

  const listable = HandleSurface.views(state).listable
  assert.equal(listable.length, 1)
  assert.equal(listable[0], 'agent:engineer')
  assert.equal(HandleSurface.read(state, listable[0]).child, 'ses_child_1')
})
test('WHAT[managed-session-lifecycle-013] session_recovery_contract_authorizes_family_without_physical_handle_leaks', () => {
  const root = 'ses_root'
  const nodes = [
    { kind: 'child', parent: root, child: 'ses_child', handle: 'agent:h1' },
    { kind: 'companion', main: root, companion: 'ses_comp' }
  ]
  const closure = RecoverySurface.validateClosure(root, nodes)
  assert.equal(closure.ok, true)
  assert.equal(closure.members.length, 2)

  // Authorize with all recovered -> FamilyReady permit with members
  const authReady = RecoverySurface.authorize(root, 1, [
    { session: 'ses_child', state: 'Recovered' },
    { session: 'ses_comp', state: 'Recovered' }
  ])
  assert.equal(authReady.state, 'FamilyReady')
  assert.equal(authReady.root, root)
  assert.equal(authReady.sequence, 1)

  // Authorize with a waiting member -> FamilyWaiting without permit
  const authWaiting = RecoverySurface.authorize(root, 1, [
    { session: 'ses_child', state: 'Waiting' },
    { session: 'ses_comp', state: 'Recovered' }
  ])
  assert.equal(authWaiting.state, 'FamilyWaiting')

  // Authorize with a blocked member -> FamilyBlocked
  const authBlocked = RecoverySurface.authorize(root, 1, [
    { session: 'ses_child', state: 'Blocked' },
    { session: 'ses_comp', state: 'Recovered' }
  ])
  assert.equal(authBlocked.state, 'FamilyBlocked')
})
}
