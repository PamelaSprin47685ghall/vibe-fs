// tests/integration/package/import.test.mjs — import package main from workspace root.
//
// Assumes package already installed / built. No npm pack/install in tests.

import assert from 'node:assert/strict'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath, pathToFileURL } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const main = path.join(repoRoot, 'dist', 'Infrastructure', 'OpenCode', 'Plugin', 'Plugin.js')

test('PACKAGE_import_wanxiangshu_main_exits_zero', async () => {
  const mod = await import(pathToFileURL(main).href)
  assert.equal(typeof mod, 'object')
  assert.ok(mod !== null)
})
