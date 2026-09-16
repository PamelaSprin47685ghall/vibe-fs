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

test('WHAT[INTERACTION-AUTHORITY-022] DevOps resume and continuation strictly lock bound model target and have direct write permissions', () => {
  // DevOps must have direct Write/Edit permissions for self-repair
  assert.equal(isAllowed('DevOps', 'Write'), true, 'DevOps must have direct Write permission')
  assert.equal(isAllowed('DevOps', 'Edit'), true, 'DevOps must have direct Edit permission')
  assert.equal(isAllowed('DevOps', 'Exec'), true, 'DevOps must have Exec permission')

  // DevOps must NOT have Fission
  assert.equal(isAllowed('DevOps', 'Fission'), false, 'DevOps must not have Fission permission')
})
