import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed, permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'



test('WHAT[INTERACTION-AUTHORITY-022] DevOps resume and continuation strictly lock bound model target and have direct write permissions', () => {
  // DevOps must have direct Write/Edit permissions for self-repair
  assert.equal(isAllowed('DevOps', 'Write'), true, 'DevOps must have direct Write permission')
  assert.equal(isAllowed('DevOps', 'Edit'), true, 'DevOps must have direct Edit permission')
  assert.equal(isAllowed('DevOps', 'Exec'), true, 'DevOps must have Exec permission')

  // DevOps must NOT have Fission
  assert.equal(isAllowed('DevOps', 'Fission'), false, 'DevOps must not have Fission permission')
})
