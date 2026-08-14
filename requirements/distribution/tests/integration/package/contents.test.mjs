// requirements/distribution/tests/integration/package/contents.test.mjs — pack membership from package.json files.
//
// No `npm pack` / `npm install` in tests. Membership is the files whitelist + required paths
// that must exist on disk for a pack to be meaningful.

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
  'reviewer',
]

const exists = (rel) => fs.existsSync(path.join(repoRoot, rel))

test('PACKAGE_contents_tarball_includes_manifest_dist_resources', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
  assert.ok(Array.isArray(pkg.files))
  assert.ok(pkg.files.some((f) => f === 'dist' || f === 'dist/' || f.startsWith('dist')))
  assert.ok(pkg.files.some((f) => f === 'resources' || f === 'resources/' || f.startsWith('resources')))

  for (const required of [
    'package.json',
    'README.md',
    'LICENSE',
    'dist/OpenCode/Plugin/Plugin.js',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
    'resources/provider/world/common-law/en.md',
    'resources/provider/world/common-law/zh-CN.md',
  ]) {
    assert.ok(exists(required), `package must include ${required}`)
  }

  assert.equal(
    exists('resources/enforcer/catalog.json'),
    false,
    'catalog.json must not ship after rulebook folder cutover',
  )
  assert.equal(
    exists('resources/prompts'),
    false,
    'legacy resources/prompts must not ship after Prompt Restoration cutover',
  )

  for (const role of PROVIDER_ROLES) {
    assert.ok(exists(`resources/provider/role/${role}/en.md`), `package must include Role Law ${role}/en.md`)
    assert.ok(
      exists(`resources/provider/role/${role}/zh-CN.md`),
      `package must include Role Law ${role}/zh-CN.md`,
    )
  }
})

test('PACKAGE_contents_tarball_excludes_source_tests_docs_scripts', () => {
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
