import assert from 'node:assert/strict'
import test from 'node:test'

const persona = await import('../../../dist/Participant/Persona/Surface.js')

test('WHAT[participant-identity-011] role labels retain their catalog spelling for current and historical roles', () => {
  const labels = {
    Manager: 'manager', Orchestrator: 'orchestrator', Engineer: 'engineer',
    DevOps: 'devops', Blogger: 'blogger', Coder: 'coder', Inspector: 'inspector',
    Browser: 'browser', Inquiry: 'inquiry', Distiller: 'distiller',
  }
  for (const [role, label] of Object.entries(labels)) {
    assert.equal(persona.roleName(role), label)
  }
  for (const invalid of ['renamed-case', null, undefined]) {
    assert.equal(persona.roleName(invalid), '')
  }
})
