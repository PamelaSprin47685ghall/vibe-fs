import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

import { REPO_ROOT } from '../../../scripts/verify-package.mjs'

const root = REPO_ROOT
const exists = (relative) => fs.existsSync(path.join(root, relative))

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_enforcer_rulebook_closure_is_complete', () => {
  const enforcerRoot = path.join(root, 'resources', 'enforcer')
  const tipDirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(tipDirs.length >= 1, 'rulebook must contain at least one tip directory')
  for (const tip of tipDirs) {
    assert.ok(exists(`resources/enforcer/${tip}/enforcer.md`), `missing enforcer.md for ${tip}`)
    assert.ok(exists(`resources/enforcer/${tip}/main.md`), `missing main.md for ${tip}`)
  }
})

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_provider_resource_closure_is_language_complete', () => {
  const roleRoot = path.join(root, 'resources', 'provider', 'role')
  const roles = fs
    .readdirSync(roleRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(roles.length >= 1, 'provider role tree must contain at least one role')
  for (const role of roles) {
    assert.ok(exists(`resources/provider/role/${role}/en.md`), `missing Role Law ${role}/en.md`)
    assert.ok(
      exists(`resources/provider/role/${role}/zh-CN.md`),
      `missing Role Law ${role}/zh-CN.md`,
    )
  }
  for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
    assert.ok(exists(`resources/provider/${leaf}/en.md`), `missing provider asset ${leaf}/en.md`)
    assert.ok(
      exists(`resources/provider/${leaf}/zh-CN.md`),
      `missing provider asset ${leaf}/zh-CN.md`,
    )
  }
})
