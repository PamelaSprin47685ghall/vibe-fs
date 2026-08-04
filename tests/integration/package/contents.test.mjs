// tests/integration/package/contents.test.mjs — npm pack tarball membership.
//
// Verifies package files whitelist/blacklist after npm pack --json.
// Requires dist/ built (npm run build). Standalone:
//   node --test tests/integration/package/contents.test.mjs

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

/** Normalize npm pack file path to package/<path> form. */
const asPackagePath = (p) => {
  const n = String(p).replace(/\\/g, '/')
  return n.startsWith('package/') ? n : `package/${n}`
}

const packOnce = (packDir) => {
  const raw = execFileSync(
    'npm',
    ['pack', '--json', '--pack-destination', packDir, '--no-audit', '--no-fund'],
    { cwd: repoRoot, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 },
  )
  const parsed = JSON.parse(raw)
  const entry = Array.isArray(parsed) ? parsed[0] : parsed
  assert.ok(entry && typeof entry === 'object', 'npm pack --json must yield an object')
  assert.equal(typeof entry.filename, 'string')
  assert.ok(entry.filename.endsWith('.tgz'), `filename must be .tgz, got ${entry.filename}`)
  assert.ok(Array.isArray(entry.files), 'npm pack --json must include files[]')
  return {
    filename: entry.filename,
    tarball: path.join(packDir, entry.filename),
    paths: new Set(entry.files.map((f) => asPackagePath(f.path))),
  }
}

test('PACKAGE_contents_tarball_includes_manifest_dist_resources', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-contents-'))
  try {
    const { paths, tarball } = packOnce(packDir)
    assert.ok(fs.existsSync(tarball), `tarball missing: ${tarball}`)

    for (const required of [
      'package/package.json',
      'package/README.md',
      'package/LICENSE',
      'package/dist/Infrastructure/OpenCode/Plugin/Plugin.js',
      'package/resources/enforcer/catalog.json',
    ]) {
      assert.ok(paths.has(required), `tarball must include ${required}`)
    }

    for (const name of PROMPT_FILES) {
      const p = `package/resources/prompts/${name}`
      assert.ok(paths.has(p), `tarball must include ${p}`)
    }

    assert.ok(
      [...paths].some((p) => p.startsWith('package/dist/')),
      'tarball must include package/dist/**',
    )
    assert.ok(
      [...paths].some((p) => p.startsWith('package/resources/')),
      'tarball must include package/resources/**',
    )
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
  }
})

test('PACKAGE_contents_tarball_excludes_source_tests_docs_scripts', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-exclude-'))
  try {
    const { paths } = packOnce(packDir)
    const bannedPrefixes = [
      'package/src/',
      'package/tests/',
      'package/scripts/',
      'package/spec/',
      'package/docs/',
      'package/artifacts/',
      'package/.git',
    ]
    for (const p of paths) {
      for (const banned of bannedPrefixes) {
        assert.ok(!p.startsWith(banned), `tarball must exclude ${banned}*, found ${p}`)
      }
      assert.ok(!p.endsWith('.fs'), `tarball must exclude *.fs, found ${p}`)
      assert.ok(!p.endsWith('.fsproj'), `tarball must exclude *.fsproj, found ${p}`)
    }
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
  }
})
