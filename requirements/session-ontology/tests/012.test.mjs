import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

test('WHAT[session-ontology-012] HOST_008_bookkeeper_attachment_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
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

test('WHAT[session-ontology-012] HOST_008_root_and_attached_helpers_are_explicit', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.equal(assoc.ownershipAttached('ses_owner', 'SyncInspector').owner, 'ses_owner')
})
test('WHAT[session-ontology-012] HOST_008_bookkeeper_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
})
test('WHAT[session-ontology-012] Bookkeeper is private identity, not a Foundation role', () => {
  assert.equal(persona.isManagedName('bookkeeper'), true)
  assert.equal(roles.allRoleLabels.includes('bookkeeper'), false)
  assert.equal(persona.nameOf('deep', 'bookkeeper'), '')
})
}
