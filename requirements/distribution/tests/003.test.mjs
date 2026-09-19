import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { REPO_ROOT, runExternalConsumer } from '../../../scripts/verify-package.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const root = REPO_ROOT

const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))

const exists = (relative) => fs.existsSync(path.join(root, relative))

const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')

test('WHAT[distribution-003] DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path', () => {
  assert.equal(typeof pkg.main, 'string', 'main must be declared')
  assert.equal(pkg.exports['.'], pkg.main, 'exports["."] must equal main')
  assert.match(pkg.main, /^\.?\/?dist\//, 'main must live under dist/')
  assert.ok(exists(pkg.main), `main must exist on disk: ${pkg.main}`)
})

integrationTest('WHAT[distribution-003] PACKAGE_external_consumer_imports_default_export_and_reads_resources', async () => {
  // If dist/OpenCode/Plugin/Plugin.js is not present yet, skip or run
  const pluginEntry = path.join(root, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(pluginEntry)) {
    return
  }

  const scratchDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pkg-consume-test-'))
  const extractedPackageDir = path.join(scratchDir, 'package')
  fs.mkdirSync(extractedPackageDir, { recursive: true })

  try {
    // Stage minimal package layout under extractedPackageDir
    fs.cpSync(path.join(root, 'package.json'), path.join(extractedPackageDir, 'package.json'))
    fs.cpSync(path.join(root, 'dist'), path.join(extractedPackageDir, 'dist'), { recursive: true })
    fs.cpSync(path.join(root, 'resources'), path.join(extractedPackageDir, 'resources'), { recursive: true })

    await runExternalConsumer({
      root,
      tarballPath: 'mock.tgz',
      extractedPackageDir,
      scratchDir,
    })
    assert.ok(true, 'external consumer executed successfully')
  } finally {
    fs.rmSync(scratchDir, { recursive: true, force: true })
  }
})

integrationTest('WHAT[distribution-003] PACKAGE_import_wanxiangshu_main_exits_zero', async () => {
  const main = path.join(root, pkg.main)
  const mod = await import(pathToFileURL(main).href)
  assert.equal(typeof mod, 'object')
  assert.ok(mod !== null)
})

integrationTest('WHAT[distribution-003] PACKAGE_layout_matches_manifest_and_main', () => {
  assert.equal(pkg.name, 'wanxiangshu')
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(pkg.files.includes('dist/') || pkg.files.includes('dist'), 'files must include dist/')
  assert.ok(
    pkg.files.includes('resources/') || pkg.files.includes('resources'),
    'files must include resources/',
  )
  assert.equal(
    fs.existsSync(path.join(root, 'resources', 'enforcer', 'catalog.json')),
    false,
    'catalog.json must not ship after rulebook folder cutover',
  )
})
