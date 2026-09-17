import assert from 'node:assert/strict'
import { readdirSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

test('WHAT[DISTRIBUTION-010] distribution artifact contains active registrations and surface consistency', async () => {
  const roleDir = join(ROOT, 'resources/provider/role')
  assert.ok(existsSync(roleDir), 'resources/provider/role directory must exist')
  const roles = readdirSync(roleDir)

  // Active role directories must include engineer and devops
  assert.ok(roles.includes('engineer'), 'resources/provider/role must include engineer')
  assert.ok(roles.includes('devops'), 'resources/provider/role must include devops')
  assert.ok(roles.includes('manager'), 'resources/provider/role must include manager')

  // Deprecated roles must be removed from resources/provider/role
  assert.equal(roles.includes('coder'), false, 'deprecated coder role directory must be removed')
  assert.equal(roles.includes('inspector'), false, 'deprecated inspector role directory must be removed')
  assert.equal(roles.includes('browser'), false, 'deprecated browser role directory must be removed')
  assert.equal(roles.includes('inquiry'), false, 'deprecated inquiry role directory must be removed')
  assert.equal(roles.includes('distiller'), false, 'deprecated distiller role directory must be removed')

  // Tool surfaces consistency: js-engineer must exist, js-coder/js-inspector must be removed
  const rolesSurface = await import('../../../dist/Foundation/RolesSurface.js')
  assert.ok(rolesSurface.allRoleLabels.includes('engineer'), 'Roles surface must contain engineer')
  assert.equal(rolesSurface.allRoleLabels.includes('coder'), false, 'Roles surface must not contain coder')
  assert.equal(rolesSurface.allRoleLabels.includes('inspector'), false, 'Roles surface must not contain inspector')
})
