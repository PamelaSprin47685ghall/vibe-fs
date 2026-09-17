import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed, permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'



test('WHAT[INTERACTION-AUTHORITY-021] historical inspector records are isolated and do not upgrade to engineer authority', () => {
  // Engineer must be an active role that has Write and Fission permissions
  assert.equal(isAllowed('Engineer', 'Write'), true, 'Engineer must have Write permission')
  assert.equal(isAllowed('Engineer', 'Fission'), true, 'Engineer must have Fission permission')

  // Manager must NOT have Fission permission
  assert.equal(isAllowed('Manager', 'Fission'), false, 'Manager must not have Fission permission')

  // Legacy Inspector must NOT have Write permission
  assert.equal(isAllowed('Inspector', 'Write'), false, 'Legacy Inspector must not have Write permission')
})
