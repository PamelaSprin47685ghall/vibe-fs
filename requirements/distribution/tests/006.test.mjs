import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { REPO_ROOT } from '../../../scripts/verify-package.mjs'
import { readText } from '../../../dist/Resources/PackageResources.js'

test('WHAT[distribution-006] production resource loader throws for a missing resource while an existing resource is readable', () => {
  assert.ok(readText('provider/role/manager/en.md').trim().length > 0)
  assert.throws(() => readText('enforcer/does-not-exist-rule/enforcer.md'), /package resource missing/)
  assert.equal(existsSync(join(REPO_ROOT, 'resources/enforcer/catalog.json')), false)
})

test.todo('WHAT[distribution-006] GAP-210: every production resource reader uses the sole infrastructure owner and missing-resource failure reaches its caller')
