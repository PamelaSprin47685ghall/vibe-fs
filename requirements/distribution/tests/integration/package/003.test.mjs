// requirements/distribution/tests/integration/package/003.test.mjs
// DISTRIBUTION-003: Package consumption, import and layout verification.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { REPO_ROOT, runExternalConsumer } from '../../../scripts/verify-package.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
const main = path.join(repoRoot, pkg.main)

test('WHAT[DISTRIBUTION-003] PACKAGE_external_consumer_imports_default_export_and_reads_resources', async () => {
  const pluginEntry = path.join(REPO_ROOT, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(pluginEntry)) {
    return
  }

  const scratchDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pkg-consume-test-'))
  const extractedPackageDir = path.join(scratchDir, 'package')
  fs.mkdirSync(extractedPackageDir, { recursive: true })

  try {
    fs.cpSync(path.join(REPO_ROOT, 'package.json'), path.join(extractedPackageDir, 'package.json'))
    fs.cpSync(path.join(REPO_ROOT, 'dist'), path.join(extractedPackageDir, 'dist'), { recursive: true })
    fs.cpSync(path.join(REPO_ROOT, 'resources'), path.join(extractedPackageDir, 'resources'), { recursive: true })

    await runExternalConsumer({
      root: REPO_ROOT,
      tarballPath: 'mock.tgz',
      extractedPackageDir,
      scratchDir,
    })
    assert.ok(true, 'external consumer executed successfully')
  } finally {
    fs.rmSync(scratchDir, { recursive: true, force: true })
  }
})

test('WHAT[DISTRIBUTION-003] PACKAGE_import_wanxiangshu_main_exits_zero', async () => {
  const mod = await import(pathToFileURL(main).href)
  assert.equal(typeof mod, 'object')
  assert.ok(mod !== null)
})

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
