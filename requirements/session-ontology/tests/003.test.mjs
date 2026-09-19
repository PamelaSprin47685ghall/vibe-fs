import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

test('WHAT[session-ontology-003] EXEC_026_dedicated_sync_roles_are_work_plus_attached', () => {
  assert.equal(assoc.dedicatedExecutionClass, 'Work')
  assert.deepEqual(assoc.dedicatedOwnership('ses_owner', 'Inspector'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncInspector', transactionId: null,
  })
  assert.deepEqual(assoc.dedicatedOwnership('ses_owner', 'Coder'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncCoder', transactionId: null,
  })
  assert.equal(assoc.dedicatedAttachment('Inspector'), 'SyncInspector')
  assert.equal(assoc.dedicatedAttachment('Coder'), 'SyncCoder')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const roles = await import("../../../dist/Foundation/RolesSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: resolved.identity.name,
      role: resolved.identity.role,
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const syncDelegateRoles = ['Inspector', 'Coder']
assert.deepEqual(syncDelegateRoles, ['Inspector', 'Coder'])

test('WHAT[session-ontology-003] HOST_008_delegate_role_maps_to_attachment', () => {
  assert.equal(assoc.dedicatedAttachment('Inspector'), 'SyncInspector')
  assert.equal(assoc.dedicatedAttachment('Coder'), 'SyncCoder')
})
}
