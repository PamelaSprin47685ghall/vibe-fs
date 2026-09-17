import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_carries_one_owner_and_one_kind', () => {
  const view = assoc.classify('ses_blogger', state)
  assert.equal(view.ownership.kind, 'Attached')
  assert.equal(typeof view.ownership.owner, 'string')
  assert.equal(view.ownership.attachment, 'Companion')
})
test('WHAT[SESSION-ONTOLOGY-002] HOST_008_root_and_attached_helpers_are_plain_views', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_o', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_o', attachment: 'SyncCoder', transactionId: null,
  })
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

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_ownership_carries_owner_and_kind', () => {
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncInspector'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncInspector', transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncCoder', transactionId: null,
  })
})
}
