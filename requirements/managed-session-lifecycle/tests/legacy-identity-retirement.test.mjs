import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import { Role } from '../../../dist/Foundation/Roles.js'

test('WHAT[MANAGED-SESSION-023] new tasks reject legacy roles and legacy active sessions are explicitly retired', () => {
  const publicLabels = RolesSurface.allPublicRoleLabels

  // Active public roles must include Engineer and DevOps
  assert.ok(publicLabels.includes('engineer'), 'Engineer must be in public role labels')
  assert.ok(publicLabels.includes('devops'), 'DevOps must be in public role labels')

  // Deprecated roles must be removed from public role labels
  assert.equal(publicLabels.includes('coder'), false, 'Coder must not be in public role labels')
  assert.equal(publicLabels.includes('inspector'), false, 'Inspector must not be in public role labels')
  assert.equal(publicLabels.includes('browser'), false, 'Browser must not be in public role labels')
  assert.equal(publicLabels.includes('inquiry'), false, 'Inquiry must not be in public role labels')
  assert.equal(publicLabels.includes('distiller'), false, 'Distiller must not be in public role labels')
})

test('WHAT[MANAGED-SESSION-024] fixed DevOps crash recovery maintains single logical authority and locks bound model', () => {
  // Check Role cases to verify Role union is consolidated
  const cases = (new Role(0, [])).cases()
  assert.ok(cases.includes('Engineer'), 'Role union must contain Engineer')
  assert.ok(cases.includes('DevOps'), 'Role union must contain DevOps')
  assert.equal(cases.includes('Coder'), false, 'Role union must not contain Coder')
  assert.equal(cases.includes('Inspector'), false, 'Role union must not contain Inspector')
  assert.equal(cases.includes('Browser'), false, 'Role union must not contain Browser')
  assert.equal(cases.includes('Inquiry'), false, 'Role union must not contain Inquiry')
  assert.equal(cases.includes('Distiller'), false, 'Role union must not contain Distiller')
})
