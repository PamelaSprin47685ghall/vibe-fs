import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[office-capability-017] DevOps has source repair and real execution permissions without delegation or Fission', () => {
  for (const permission of ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Pty']) {
    assert.equal(isAllowed('devops', permission), true, permission)
  }
  for (const permission of ['Fork', 'Resume', 'Fission']) {
    assert.equal(isAllowed('devops', permission), false, permission)
  }
})

test.todo('WHAT[office-capability-017] real defect repair must retain product boundaries and revalidate the changed source; a permission set or closing an empty PTY port does not prove this workflow')
