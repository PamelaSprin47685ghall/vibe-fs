// requirements/capability-enforcement/tests/inquiry-permissions.test.mjs
//
// ENF-006 / ENF-007 / OFF-018: Inquiry role revocation and Sphinx programmatic workflow.
// Inquiry role is revoked from canonical active roles. Its permissions fail closed to empty set.
// Sphinx is programmatic workflow without Inquiry driver persona.

import assert from 'node:assert/strict'
import test from 'node:test'

import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { permissions as rolePermissions, isAllowed as surfaceIsAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[ENF-006] inquiry_role_is_revoked_and_permissions_fail_closed', () => {
  assert.equal(allRoleLabels.includes('inquiry'), false, 'Inquiry must not be in canonical active roles')
  const allowed = rolePermissions('inquiry')
  assert.deepEqual(allowed, [], 'inquiry permissions must fail closed to empty set')
})

test('WHAT[ENF-006] inquiry_isAllowed_denies_all_tools', () => {
  assert.equal(surfaceIsAllowed('inquiry', 'Inspect'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Sphinx'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Fission'), false)
  assert.equal(surfaceIsAllowed('inquiry', 'Read'), false)
})

test('WHAT[ENF-010] inquiry_rolePredicate_denies_all_tools', () => {
  assert.equal(rolePredicate('inspect', 'inquiry'), false)
  assert.equal(rolePredicate('fission', 'inquiry'), false)
})
