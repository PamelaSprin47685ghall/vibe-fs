import assert from 'node:assert/strict'
import { readFileSync, existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
import test from 'node:test'
import {
  REPO_ROOT,
  ROOT_FILE_WHITELIST,
  ROOT_OPTIONAL_FILES,
  ROOT_REQUIRED_FILES,
  deriveExpectedClosure,
} from '../../../scripts/verify-package.mjs'

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

test('WHAT[distribution-003] the manifest is a contract-required package-root member of the declared closure', () => {
  assert.deepEqual(ROOT_REQUIRED_FILES, ['package.json'])
  assert.deepEqual(ROOT_FILE_WHITELIST, [...ROOT_REQUIRED_FILES, ...ROOT_OPTIONAL_FILES])

  const closure = deriveExpectedClosure()
  for (const required of ROOT_REQUIRED_FILES) {
    assert.ok(existsSync(join(REPO_ROOT, required)), `required root member missing: ${required}`)
    assert.ok(closure.has(required), `required root member absent from closure: ${required}`)
  }
  for (const optional of ROOT_OPTIONAL_FILES) {
    assert.equal(
      closure.has(optional),
      existsSync(join(REPO_ROOT, optional)),
      `optional root member ${optional} is allowed exactly when the source tree has it`,
    )
  }
})

test('WHAT[distribution-003] a source tree without the required root member fails instead of shrinking the closure', () => {
  const bareRoot = mkdtempSync(join(tmpdir(), 'wxs-required-root-'))
  try {
    assert.throws(
      () => deriveExpectedClosure({ root: bareRoot }),
      (err) => err.code === 'missing-root-member' && err.path === 'package.json',
      'an absent required root member must be a contract failure',
    )
    writeFileSync(join(bareRoot, 'package.json'), '{"name":"probe"}\n')
    assert.ok(
      deriveExpectedClosure({ root: bareRoot }).has('package.json'),
      'the required root member enters the closure once present',
    )
  } finally {
    rmSync(bareRoot, { recursive: true, force: true })
  }
})
