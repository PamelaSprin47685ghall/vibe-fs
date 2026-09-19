import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const root = process.cwd()

const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))

const exists = (relative) => fs.existsSync(path.join(root, relative))

const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')

test('WHAT[distribution-004] DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy', () => {
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'dist'),
    'files whitelist must include dist/ (compiled runtime code)',
  )
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'resources'),
    'files whitelist must include resources/ (runtime semantic resources)',
  )
  for (const entry of pkg.files) {
    const normalized = normalize(entry)
    for (const banned of ['src', 'tests', 'scripts', 'docs', 'artifacts', 'spec']) {
      assert.ok(
        normalized !== banned && !normalized.startsWith(`${banned}/`),
        `files whitelist must not ship ${banned}*, found ${entry}`,
      )
    }
    assert.ok(
      !normalized.endsWith('.fs') && !normalized.endsWith('.fsproj'),
      `files whitelist must not ship F# sources, found ${entry}`,
    )
  }
})

integrationTest('WHAT[distribution-004] PACKAGE_contents_tarball_excludes_source_tests_docs_scripts', () => {
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
