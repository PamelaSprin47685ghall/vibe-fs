// requirements/distribution/tests/integration/package/layout.test.mjs
// Package layout manifest declaration test.
// Full package layout and archive verification is governed by verifyPackage.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))

test('WHAT[DISTRIBUTION-003] PACKAGE_layout_matches_manifest_and_main', () => {
  assert.equal(pkg.name, 'wanxiangshu')
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(pkg.files.includes('dist/') || pkg.files.includes('dist'), 'files must include dist/')
  assert.ok(
    pkg.files.includes('resources/') || pkg.files.includes('resources'),
    'files must include resources/',
  )
  assert.equal(
    fs.existsSync(path.join(repoRoot, 'resources', 'enforcer', 'catalog.json')),
    false,
    'catalog.json must not ship after rulebook folder cutover',
  )
})
