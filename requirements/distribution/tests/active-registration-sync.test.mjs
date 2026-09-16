import assert from 'node:assert/strict'
import { readdirSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

test('WHAT[DISTRIBUTION-010] distribution artifact contains active registrations and surface consistency', () => {
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
  const jsDir = join(ROOT, 'src/Wanxiangshu/Repository/Programming/Js')
  if (existsSync(jsDir)) {
    const jsFiles = readdirSync(jsDir)
    assert.ok(jsFiles.some((f) => f.includes('Engineer')), 'Js Programming directory must contain Engineer surface')
    assert.equal(jsFiles.some((f) => f.includes('Coder')), false, 'Js Programming directory must not contain Coder surface')
    assert.equal(jsFiles.some((f) => f.includes('Inspector')), false, 'Js Programming directory must not contain Inspector surface')
  }
})
