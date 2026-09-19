import assert from 'node:assert/strict'
import crypto from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  REPO_ROOT,
  deriveExpectedClosure,
  validateArchiveEntries,
  validateArtifact,
} from '../../../scripts/verify-package.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const root = REPO_ROOT

const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))

const exists = (relative) => fs.existsSync(path.join(root, relative))

const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')

const walkFs = (dir) => {
  const out = []
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) out.push(...walkFs(full))
    else if (entry.isFile() && entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

const PROVIDER_ROLES = [
  'engineer',
  'manager',
  'orchestrator',
  'devops',
  'blogger',
  'bookkeeper',
]

test('WHAT[distribution-008] DISTRIBUTION_enforcer_rulebook_closure_is_complete', () => {
  const enforcerRoot = path.join(root, 'resources', 'enforcer')
  const tipDirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(tipDirs.length >= 1, 'rulebook must contain at least one tip directory')
  for (const tip of tipDirs) {
    assert.ok(exists(`resources/enforcer/${tip}/enforcer.md`), `missing enforcer.md for ${tip}`)
    assert.ok(exists(`resources/enforcer/${tip}/main.md`), `missing main.md for ${tip}`)
  }
})

test('WHAT[distribution-008] DISTRIBUTION_provider_resource_closure_is_language_complete', () => {
  const roleRoot = path.join(root, 'resources', 'provider', 'role')
  const roles = fs
    .readdirSync(roleRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(roles.length >= 1, 'provider role tree must contain at least one role')
  for (const role of roles) {
    assert.ok(exists(`resources/provider/role/${role}/en.md`), `missing Role Law ${role}/en.md`)
    assert.ok(
      exists(`resources/provider/role/${role}/zh-CN.md`),
      `missing Role Law ${role}/zh-CN.md`,
    )
  }
  for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
    assert.ok(exists(`resources/provider/${leaf}/en.md`), `missing provider asset ${leaf}/en.md`)
    assert.ok(
      exists(`resources/provider/${leaf}/zh-CN.md`),
      `missing provider asset ${leaf}/zh-CN.md`,
    )
  }
})

integrationTest('WHAT[distribution-008] PACKAGE_resources_provider_role_laws_and_rulebook_present_after_install', () => {
  const providerDir = path.join(root, 'resources', 'provider')
  const enforcerDir = path.join(root, 'resources', 'enforcer')

  assert.ok(fs.existsSync(providerDir), 'resources/provider must exist')
  assert.equal(
    fs.existsSync(path.join(root, 'resources', 'prompts')),
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
  assert.equal(PROVIDER_ROLES.length, 6)

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
