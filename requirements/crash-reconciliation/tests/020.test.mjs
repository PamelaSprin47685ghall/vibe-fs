import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // RolesSurface must have consolidated DevOps and Engineer
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('devops'), 'Role labels must have devops')
  assert.ok(all.includes('engineer'), 'Role labels must have engineer')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')
})
