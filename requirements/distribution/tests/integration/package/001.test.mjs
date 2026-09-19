import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')

test('WHAT[distribution-001] PACKAGE_contents_tarball_includes_manifest_dist_resources', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
  assert.ok(Array.isArray(pkg.files))
  assert.ok(pkg.files.some((f) => f === 'dist' || f === 'dist/' || f.startsWith('dist')))
  assert.ok(pkg.files.some((f) => f === 'resources' || f === 'resources/' || f.startsWith('resources')))
})
