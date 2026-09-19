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

test('WHAT[distribution-001] DISTRIBUTION_artifact_carries_compiled_code_and_runtime_resources_together', () => {
  const required = [
    'dist/OpenCode/Plugin/Plugin.js',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
  ]
  for (const relative of required) {
    assert.ok(exists(relative), `artifact must carry ${relative}`)
  }
  const entry = fs.readFileSync(path.join(root, 'dist/OpenCode/Plugin/Plugin.js'), 'utf8')
  assert.ok(entry.trim().length > 0, 'compiled entrypoint must be non-empty')
  for (const relative of required.filter((r) => r.startsWith('resources/'))) {
    const text = fs.readFileSync(path.join(root, relative), 'utf8')
    assert.ok(text.trim().length > 0, `runtime semantic resource must be non-empty: ${relative}`)
  }
  assert.ok(Array.isArray(pkg.files), 'files whitelist must exist')
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'dist') &&
      pkg.files.some((f) => normalize(f) === 'resources'),
    'one artifact must ship compiled code and runtime resources together (files whitelist)',
  )
})
