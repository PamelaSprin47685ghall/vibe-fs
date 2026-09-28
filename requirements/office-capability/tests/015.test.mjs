import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed, managerForkableOffices, permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[office-capability-015] Predictor has no public office permission projection or Manager fork candidate', () => {
  assert.equal(managerForkableOffices().includes('predictor'), false)
  assert.deepEqual(permissions('predictor'), [])
  for (const permission of ['Fork', 'Resume', 'Read', 'Write', 'Exec', 'Fission']) {
    assert.equal(isAllowed('predictor', permission), false)
  }
})

test.todo('WHAT[office-capability-015] Predictor confinement to internal Strength must be proved at scheduling and visibility boundaries after resolving 07-D2')
