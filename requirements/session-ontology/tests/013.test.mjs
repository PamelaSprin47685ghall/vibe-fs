import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

test('WHAT[session-ontology-013] HOST_008_canonical_role_label_is_catalog_stable', () => {
  for (const role of ['Manager', 'Coder', 'Orchestrator']) {
    assert.equal(persona.roleName(role), role.toLowerCase())
  }
  assert.equal(persona.roleName('renamed-case'), '')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const persona = await import("../../../dist/Participant/Persona/Surface.js");


test('WHAT[session-ontology-013] TPOL_roleName_uses_catalog_labels_and_rejects_none', () => {
  assert.equal(persona.roleName('Manager'), 'manager')
  assert.equal(persona.roleName('Coder'), 'coder')
  assert.equal(persona.roleName('Orchestrator'), 'orchestrator')
  assert.equal(persona.roleName(null), '')
  assert.equal(persona.roleName(undefined), '')
})
}
