import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'



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
