import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[office-capability-012] the Orchestrator projection grants orchestration without direct execution or Fission', () => {
  assert.equal(isAllowed('orchestrator', 'Fork'), true)
  for (const permission of ['Read', 'Write', 'Edit', 'Exec', 'Pty', 'Fission']) {
    assert.equal(isAllowed('orchestrator', permission), false, permission)
  }
})

test.todo('WHAT[office-capability-012] actual Orchestrator admission must accept only Manager targets; a generic Fork permission does not establish the target boundary')
