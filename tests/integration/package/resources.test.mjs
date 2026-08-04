// tests/integration/package/resources.test.mjs — installed resources layout.
//
// After consumer install, every packaged prompt + enforcer catalog is present,
// non-empty, and catalog.json parses. Fixed relative layout matches
// PackageResources (dist/Infrastructure/Resources → ../../../resources).
// Requires dist/ built. Network may be needed for package dependencies.
//   node --test tests/integration/package/resources.test.mjs

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

const PROMPT_FILES = [
  'blogger-system.md',
  'browser-system.md',
  'coder-system.md',
  'devops-system.md',
  'executor-system.md',
  'inspector-system.md',
  'manager-system.md',
  'meditator-system.md',
  'orchestrator-system.md',
  'reviewer-system.md',
]

const packTarball = (packDir) => {
  const raw = execFileSync(
    'npm',
    ['pack', '--json', '--pack-destination', packDir, '--no-audit', '--no-fund'],
    { cwd: repoRoot, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 },
  )
  const entry = JSON.parse(raw)[0]
  const tarball = path.join(packDir, entry.filename)
  assert.ok(fs.existsSync(tarball), `tarball missing: ${tarball}`)
  return tarball
}

const installConsumer = (consumerDir, tarball) => {
  fs.writeFileSync(
    path.join(consumerDir, 'package.json'),
    JSON.stringify({ name: 'consumer', private: true, type: 'module' }),
    'utf8',
  )
  execFileSync(
    'npm',
    ['install', tarball, '--no-audit', '--no-fund'],
    {
      cwd: consumerDir,
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
      env: { ...process.env, npm_config_fund: 'false', npm_config_audit: 'false' },
    },
  )
}

test('PACKAGE_resources_all_prompts_and_catalog_present_after_install', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-resources-'))
  const consumerDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-consumer-resources-'))
  try {
    const tarball = packTarball(packDir)
    installConsumer(consumerDir, tarball)

    const pkgRoot = path.join(consumerDir, 'node_modules', 'wanxiangshu')
    const promptsDir = path.join(pkgRoot, 'resources', 'prompts')
    const catalogPath = path.join(pkgRoot, 'resources', 'enforcer', 'catalog.json')

    assert.ok(fs.existsSync(promptsDir), 'resources/prompts must exist')
    for (const name of PROMPT_FILES) {
      const full = path.join(promptsDir, name)
      assert.ok(fs.existsSync(full), `missing prompt ${name}`)
      const text = fs.readFileSync(full, 'utf8')
      assert.ok(text.trim().length > 0, `prompt ${name} must be non-empty`)
    }
    assert.equal(PROMPT_FILES.length, 10)

    assert.ok(fs.existsSync(catalogPath), 'resources/enforcer/catalog.json must exist')
    const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'))
    assert.ok(catalog && typeof catalog === 'object', 'catalog.json must parse as object')
    assert.ok(Array.isArray(catalog.rules), 'catalog.json must have rules array')
    assert.ok(catalog.rules.length > 0, 'catalog.rules must be non-empty')
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
    fs.rmSync(consumerDir, { recursive: true, force: true })
  }
})

test('PACKAGE_resources_fixed_relative_path_from_PackageResources_module', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-relpath-'))
  const consumerDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-consumer-relpath-'))
  try {
    const tarball = packTarball(packDir)
    installConsumer(consumerDir, tarball)

    const pkgRoot = path.join(consumerDir, 'node_modules', 'wanxiangshu')
    const packageResourcesJs = path.join(
      pkgRoot,
      'dist',
      'Infrastructure',
      'Resources',
      'PackageResources.js',
    )
    assert.ok(fs.existsSync(packageResourcesJs), 'PackageResources.js must ship in dist')

    // Layout contract: module at dist/Infrastructure/Resources/ → ../../../resources
    const fromModule = path.resolve(path.dirname(packageResourcesJs), '../../..', 'resources')
    const expected = path.join(pkgRoot, 'resources')
    assert.equal(
      path.normalize(fromModule),
      path.normalize(expected),
      'PackageResources ../../../resources must resolve to package resources/',
    )
    assert.ok(fs.existsSync(path.join(fromModule, 'enforcer', 'catalog.json')))
    assert.ok(fs.existsSync(path.join(fromModule, 'prompts', 'manager-system.md')))
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
    fs.rmSync(consumerDir, { recursive: true, force: true })
  }
})
