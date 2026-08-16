// requirements/distribution/tests/integration/package/import.test.mjs — import package main from workspace root.
//
// Assumes package already installed / built. No npm pack/install in tests.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath, pathToFileURL } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
const main = path.join(repoRoot, pkg.main)

test('WHAT[DISTRIBUTION-003] PACKAGE_import_wanxiangshu_main_exits_zero', async () => {
  const mod = await import(pathToFileURL(main).href)
  assert.equal(typeof mod, 'object')
  assert.ok(mod !== null)
})
