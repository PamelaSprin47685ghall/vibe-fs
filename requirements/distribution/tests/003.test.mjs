import assert from 'node:assert/strict'
import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
import test from 'node:test'
import { REPO_ROOT } from '../../../scripts/verify-package.mjs'

const manifest = JSON.parse(readFileSync(join(REPO_ROOT, 'package.json'), 'utf8'))

test('WHAT[distribution-003] repository manifest names the exact compiled production entry and exports the plugin', async () => {
  assert.equal(manifest.main, './dist/OpenCode/Plugin/Plugin.js')
  assert.equal(manifest.exports['.'], manifest.main)
  const entry = join(REPO_ROOT, manifest.main)
  assert.ok(existsSync(entry))
  const loaded = await import(pathToFileURL(entry).href)
  assert.equal(loaded.default.id, 'wanxiangshu-next')
  assert.equal(typeof loaded.default.server, 'function')
})

test.todo('WHAT[distribution-003] GAP-210: installed manifest resolves inside the exact verified tarball without repository dependencies')
