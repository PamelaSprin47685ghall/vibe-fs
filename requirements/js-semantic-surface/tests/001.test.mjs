import assert from 'node:assert/strict'
import { dirname, join, relative, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { semanticImportEdges } from '../../../scripts/lib/test-surface-scan.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'
import { createWorkspaceFixture } from './support/workspace-fixture.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const otherTestCarriers = (root) => walk(root).filter((file) =>
  relative(root, file).split(/[\\/]/).includes('tests')
  && /\.test\.[^/]+$/.test(file) && !file.endsWith('.test.mjs'))

test('WHAT[js-semantic-surface-001] formal semantic test carriers use .test.mjs', () => {
  assert.deepEqual(otherTestCarriers(join(ROOT, 'requirements')), [])
})

test('WHAT[js-semantic-surface-001] the carrier check rejects a second language without confusing source fixtures with tests', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('owner/tests/001.test.mjs', '')
  write('owner/tests/fixtures/Subject.fs', 'module Subject')
  assert.deepEqual(otherTestCarriers(root), [])
  const invalid = write('owner/tests/002.test.ts', '')
  assert.deepEqual(otherTestCarriers(root), [invalid])
})

test('WHAT[js-semantic-surface-001] imported test support satisfies the required .mjs suffix', {
  todo: '03-D1: existing .js support conflicts with the current .mjs-only rule; decide the rule before migration',
}, () => {
  const targets = semanticImportEdges(join(ROOT, 'requirements'))
    .map(({ target }) => target).filter((file) => file.endsWith('.js'))
  assert.deepEqual([...new Set(targets)].map((file) => relative(ROOT, file)), [])
})
