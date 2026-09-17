import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'



test('WHAT[MANAGED-SESSION-024] fixed DevOps crash recovery maintains single logical authority and locks bound model', () => {
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('engineer'), 'Role labels must contain engineer')
  assert.ok(all.includes('devops'), 'Role labels must contain devops')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')
  assert.equal(all.includes('inspector'), false, 'Role labels must not contain inspector')
  assert.equal(all.includes('browser'), false, 'Role labels must not contain browser')
  assert.equal(all.includes('inquiry'), false, 'Role labels must not contain inquiry')
  assert.equal(all.includes('distiller'), false, 'Role labels must not contain distiller')
})
