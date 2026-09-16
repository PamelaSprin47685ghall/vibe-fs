// SESSION-ONTOLOGY proof — canonical durable role labels.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

test('WHAT[SESSION-ONTOLOGY-013] TPOL_roleName_uses_catalog_labels_and_rejects_none', () => {
  assert.equal(persona.roleName('Manager'), 'manager')
  assert.equal(persona.roleName('Coder'), 'coder')
  assert.equal(persona.roleName('Orchestrator'), 'orchestrator')
  assert.equal(persona.roleName(null), '')
  assert.equal(persona.roleName(undefined), '')
})
