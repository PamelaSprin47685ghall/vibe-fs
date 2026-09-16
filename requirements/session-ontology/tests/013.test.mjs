import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

// SESSION-ONTOLOGY proof — ExecutionClass × Ownership derived views.


const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

// SESSION-ONTOLOGY-001/002: durable links derive orthogonal, exhaustive cells.

// SESSION-ONTOLOGY proof — canonical durable role labels.

test('WHAT[SESSION-ONTOLOGY-013] HOST_008_canonical_role_label_is_catalog_stable', () => {
  for (const role of ['Manager', 'Coder', 'Orchestrator']) {
    assert.equal(persona.roleName(role), role.toLowerCase())
  }
  assert.equal(persona.roleName('renamed-case'), '')
})

test('WHAT[SESSION-ONTOLOGY-013] TPOL_roleName_uses_catalog_labels_and_rejects_none', () => {
  assert.equal(persona.roleName('Manager'), 'manager')
  assert.equal(persona.roleName('Coder'), 'coder')
  assert.equal(persona.roleName('Orchestrator'), 'orchestrator')
  assert.equal(persona.roleName(null), '')
  assert.equal(persona.roleName(undefined), '')
})
