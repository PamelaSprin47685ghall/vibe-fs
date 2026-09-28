import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { REPO_ROOT } from '../../../scripts/verify-package.mjs'

test('WHAT[distribution-004] manifest declares exactly the two runtime directories', () => {
  const manifest = JSON.parse(readFileSync(join(REPO_ROOT, 'package.json'), 'utf8'))
  assert.deepEqual(manifest.files, ['dist/', 'resources/'])
})
