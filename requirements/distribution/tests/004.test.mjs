import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const root = process.cwd()

const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))

const exists = (relative) => fs.existsSync(path.join(root, relative))

const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')

test('WHAT[DISTRIBUTION-004] DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy', () => {
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
