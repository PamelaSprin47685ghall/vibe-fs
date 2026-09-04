// requirements/distribution/tests/integration/package/resources.test.mjs — packaged resources at workspace root.
//
// Assumes package already built. No npm pack/install in tests.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')

const PROVIDER_ROLES = [
  'blogger',
  'bookkeeper',
  'browser',
  'coder',
  'devops',
  'distiller',
  'inquiry',
  'inspector',
  'manager',
  'orchestrator',
]

test('WHAT[DISTRIBUTION-008] PACKAGE_resources_provider_role_laws_and_rulebook_present_after_install', () => {
  const providerDir = path.join(repoRoot, 'resources', 'provider')
  const enforcerDir = path.join(repoRoot, 'resources', 'enforcer')

  assert.ok(fs.existsSync(providerDir), 'resources/provider must exist')
  assert.equal(
    fs.existsSync(path.join(repoRoot, 'resources', 'prompts')),
    false,
    'legacy resources/prompts must be gone after Prompt Restoration cutover',
  )

  for (const role of PROVIDER_ROLES) {
    for (const locale of ['en.md', 'zh-CN.md']) {
      const full = path.join(providerDir, 'role', role, locale)
      assert.ok(fs.existsSync(full), `missing Role Law ${role}/${locale}`)
      const text = fs.readFileSync(full, 'utf8')
      assert.ok(text.trim().length > 0, `Role Law ${role}/${locale} must be non-empty`)
    }
  }
  assert.equal(PROVIDER_ROLES.length, 10)

  for (const leaf of ['world/common-law/en.md', 'world/common-law/zh-CN.md']) {
    const full = path.join(providerDir, leaf)
    assert.ok(fs.existsSync(full), `missing provider asset ${leaf}`)
  }

  assert.ok(fs.existsSync(enforcerDir), 'resources/enforcer must exist')
  assert.equal(
    fs.existsSync(path.join(enforcerDir, 'catalog.json')),
    false,
    'catalog.json must not ship after rulebook folder cutover',
  )
  const dirs = fs
    .readdirSync(enforcerDir, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
  assert.ok(dirs.length > 0, 'rulebook must contain at least one rule directory')
  for (const name of dirs) {
    assert.ok(
      fs.existsSync(path.join(enforcerDir, name, 'enforcer.md')),
      `missing enforcer.md for ${name}`,
    )
    assert.ok(fs.existsSync(path.join(enforcerDir, name, 'main.md')), `missing main.md for ${name}`)
  }
})

test('WHAT[DISTRIBUTION-008] PACKAGE_resources_fixed_relative_path_from_PackageResources_module', () => {
  const packageResourcesJs = path.join(repoRoot, 'dist', 'Resources', 'PackageResources.js')
  assert.ok(fs.existsSync(packageResourcesJs), 'PackageResources.js must exist under dist')

  const fromModule = path.resolve(path.dirname(packageResourcesJs), '../..', 'resources')
  const expected = path.join(repoRoot, 'resources')
  assert.equal(
    path.normalize(fromModule),
    path.normalize(expected),
    'PackageResources ../../resources must resolve to package resources/',
  )
  assert.ok(fs.existsSync(path.join(fromModule, 'enforcer', 'primitive-obsession', 'enforcer.md')))
  assert.ok(fs.existsSync(path.join(fromModule, 'provider', 'role', 'manager', 'en.md')))
})
