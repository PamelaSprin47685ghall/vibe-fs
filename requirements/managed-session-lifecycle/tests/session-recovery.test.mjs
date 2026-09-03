import assert from 'node:assert/strict'
import test from 'node:test'
import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'
import * as RecoverySurface from '../../../dist/Execution/Session/Recovery/Surface.js'
import * as AssociationSurface from '../../../dist/Execution/Session/AssociationSurface.js'
import * as SatelliteSurface from '../../../dist/OpenCode/Host/SatelliteSurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[MANAGED-SESSION-003] session_recovery_contract_restart_reuses_matching_durable_association', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(true, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Reused')
  assert.deepEqual(observed.linked, [['work', 'blogger-1', 'blogger']])
})

test('WHAT[MANAGED-SESSION-003] session_recovery_contract_conflict_fails_closed_without_guessing', async () => {
  const conflict = await SatelliteSurface.SatelliteSurface_scenario(true, true, true, false)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /Conflicting companion satellite recovery/)

  const queryError = await SatelliteSurface.SatelliteSurface_scenario(false, false, false, true)
  assert.equal(queryError.ok, false)
  assert.match(queryError.error, /Cannot recover companion satellite/)
})

test('WHAT[MANAGED-SESSION-013] session_recovery_contract_reenlist_filters_hidden_handles', () => {
  let state = HandleSurface.empty()
  const parent = 'ses_parent'
  
  // Link durable public child
  const r1 = HandleSurface.apply(state, { op: 'link', handle: 'agent:coder', child: 'ses_child_1', agent: 'coder', role: 'Coder', ownership: 'DurableParentHandle' })
  state = r1.state

  // Link host-owned hidden child (e.g. distiller / reviewer)
  const r2 = HandleSurface.apply(state, { op: 'link', handle: 'agent:distiller', child: 'ses_distiller_1', agent: 'distiller', role: 'Distiller', ownership: 'HostOwnedHidden' })
  state = r2.state

  const listable = HandleSurface.views(state).listable
  assert.equal(listable.length, 1)
  assert.equal(listable[0], 'agent:coder')
  assert.equal(HandleSurface.read(state, listable[0]).child, 'ses_child_1')
})

test('WHAT[MANAGED-SESSION-013] session_recovery_contract_authorizes_family_without_physical_handle_leaks', () => {
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

test('WHAT[MANAGED-SESSION-001] session_recovery_contract_attached_runtime_single_owner_pure_evidence', async () => {
  const result = await AttachmentSurface.scenario('owner_1', 'Inspector', 'inspector', 'inspector', true)
  assert.equal(result.created, 1)
  assert.equal(result.firstChild, 'child-1')
  assert.equal(result.secondChild, 'child-1')
  assert.equal(result.firstAgent, 'inspector')
})
