// tests/integration/package/resources.test.mjs — packaged resources at workspace root.
//
// Assumes package already built. No npm pack/install in tests.

import assert from 'node:assert/strict'
import fs from 'node:fs'
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

test('PACKAGE_resources_all_prompts_and_catalog_present_after_install', () => {
  const promptsDir = path.join(repoRoot, 'resources', 'prompts')
  const catalogPath = path.join(repoRoot, 'resources', 'enforcer', 'catalog.json')

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
})

test('PACKAGE_resources_fixed_relative_path_from_PackageResources_module', () => {
  const packageResourcesJs = path.join(
    repoRoot,
    'dist',
    'Infrastructure',
    'Resources',
    'PackageResources.js',
  )
  assert.ok(fs.existsSync(packageResourcesJs), 'PackageResources.js must exist under dist')

  const fromModule = path.resolve(path.dirname(packageResourcesJs), '../../..', 'resources')
  const expected = path.join(repoRoot, 'resources')
  assert.equal(
    path.normalize(fromModule),
    path.normalize(expected),
    'PackageResources ../../../resources must resolve to package resources/',
  )
  assert.ok(fs.existsSync(path.join(fromModule, 'enforcer', 'catalog.json')))
  assert.ok(fs.existsSync(path.join(fromModule, 'prompts', 'manager-system.md')))
})
