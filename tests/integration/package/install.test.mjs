// tests/integration/package/install.test.mjs — package layout as shipped.
//
// Assumes this package is already installed for the workspace (no npm install in tests).
// Asserts the on-disk package root matches what a consumer would get from `files` + main.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))

test('PACKAGE_install_layout_matches_manifest_and_main', () => {
  assert.equal(pkg.name, 'wanxiangshu')
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(pkg.files.includes('dist/') || pkg.files.includes('dist'), 'files must include dist/')
  assert.ok(
    pkg.files.includes('resources/') || pkg.files.includes('resources'),
    'files must include resources/',
  )

  const main = path.join(repoRoot, pkg.main || 'dist/Infrastructure/OpenCode/Plugin/Plugin.js')
  assert.ok(fs.existsSync(main), `main entry must exist: ${main}`)
  assert.ok(fs.existsSync(path.join(repoRoot, 'package.json')))
  assert.ok(fs.existsSync(path.join(repoRoot, 'dist', 'Infrastructure', 'OpenCode', 'Plugin', 'Plugin.js')))
  assert.ok(fs.existsSync(path.join(repoRoot, 'resources', 'enforcer', 'catalog.json')))
})
