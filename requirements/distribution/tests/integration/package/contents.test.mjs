// requirements/distribution/tests/integration/package/contents.test.mjs
// Package whitelist boundary tests.
// Full package membership and archive digest verification is governed by verifyPackage.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')

test('WHAT[DISTRIBUTION-001] PACKAGE_contents_tarball_includes_manifest_dist_resources', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
  assert.ok(Array.isArray(pkg.files))
  assert.ok(pkg.files.some((f) => f === 'dist' || f === 'dist/' || f.startsWith('dist')))
  assert.ok(pkg.files.some((f) => f === 'resources' || f === 'resources/' || f.startsWith('resources')))
})

test('WHAT[DISTRIBUTION-004] PACKAGE_contents_tarball_excludes_source_tests_docs_scripts', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
  const files = pkg.files.map((f) => String(f).replace(/\\/g, '/'))
  const banned = ['src', 'src/', 'tests', 'tests/', 'scripts', 'scripts/', 'spec', 'spec/', 'docs', 'docs/', 'requirements', 'requirements/']
  for (const entry of files) {
    for (const b of banned) {
      assert.ok(
        entry !== b && !entry.startsWith(b.endsWith('/') ? b : `${b}/`),
        `files whitelist must not ship ${b}*, found ${entry}`,
      )
    }
    assert.ok(!entry.endsWith('.fs'), `files whitelist must not include *.fs, found ${entry}`)
    assert.ok(!entry.endsWith('.fsproj'), `files whitelist must not include *.fsproj, found ${entry}`)
  }
})
