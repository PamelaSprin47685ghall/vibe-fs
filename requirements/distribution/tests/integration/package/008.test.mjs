// requirements/distribution/tests/integration/package/008.test.mjs
// DISTRIBUTION-008: Packaged resources at workspace root.

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
})
