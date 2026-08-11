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

test('PACKAGE_resources_all_prompts_and_rulebook_present_after_install', () => {
  const promptsDir = path.join(repoRoot, 'resources', 'prompts')
  const enforcerDir = path.join(repoRoot, 'resources', 'enforcer')

  assert.ok(fs.existsSync(promptsDir), 'resources/prompts must exist')
  for (const name of PROMPT_FILES) {
    const full = path.join(promptsDir, name)
    assert.ok(fs.existsSync(full), `missing prompt ${name}`)
    const text = fs.readFileSync(full, 'utf8')
    assert.ok(text.trim().length > 0, `prompt ${name} must be non-empty`)
  }
  assert.equal(PROMPT_FILES.length, 10)

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
  assert.ok(fs.existsSync(path.join(fromModule, 'enforcer', 'primitive-obsession', 'enforcer.md')))
  assert.ok(fs.existsSync(path.join(fromModule, 'prompts', 'manager-system.md')))
})
