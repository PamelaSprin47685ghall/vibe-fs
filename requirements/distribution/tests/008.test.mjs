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

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_enforcer_rulebook_closure_is_complete', () => {
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

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_provider_resource_closure_is_language_complete', () => {
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
