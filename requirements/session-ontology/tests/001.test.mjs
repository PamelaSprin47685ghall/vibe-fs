import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.deepEqual(assoc.executionClass('Work'), { name: 'Work', isWork: true, isInternalLeaf: false })
  assert.deepEqual(assoc.executionClass('InternalLeaf'), { name: 'InternalLeaf', isWork: false, isInternalLeaf: true })
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

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.equal(assoc.executionClass('Work').isWork, true)
  assert.equal(assoc.executionClass('Work').isInternalLeaf, false)
  assert.equal(assoc.executionClass('InternalLeaf').isWork, false)
  assert.equal(assoc.executionClass('InternalLeaf').isInternalLeaf, true)
})
}
