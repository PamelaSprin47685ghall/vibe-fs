import assert from 'node:assert/strict'
import { join, relative } from 'node:path'
import test from 'node:test'
import { scanAll, semanticImportEdges } from '../../../scripts/lib/test-surface-scan.mjs'
import { createWorkspaceFixture } from './support/workspace-fixture.mjs'

test('WHAT[js-semantic-surface-004] support dependencies are traversed transitively, including cycles', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  const base = 'requirements/probe/tests/'
  write(`${base}001.test.mjs`, "import './support/a.mjs'\n")
  write(`${base}support/a.mjs`, "import './b.mjs'\n")
  write(`${base}support/b.mjs`, "import './a.mjs'\n")
  const edges = semanticImportEdges(join(root, 'requirements')).map(({ importer, target }) =>
    [relative(root, importer), relative(root, target)])
  assert.deepEqual(edges, [
    [`${base}001.test.mjs`, `${base}support/a.mjs`],
    [`${base}support/a.mjs`, `${base}support/b.mjs`],
    [`${base}support/b.mjs`, `${base}support/a.mjs`],
  ])
})

test('WHAT[js-semantic-surface-004] moving an internal import into support or leaving support unreferenced cannot hide it', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  const base = 'requirements/probe/tests/'
  write(`${base}001.test.mjs`, "import './support/a.mjs'\n")
  write(`${base}support/a.mjs`, "import './b.mjs'\n")
  write(`${base}support/b.mjs`, 'export const value = 1\n')
  assert.deepEqual(scanAll(join(root, 'requirements')), {})
  const source = ['import { hidden } from "../../../../', 'dist/Internal/Module.js"\n'].join('')
  const referenced = write(`${base}support/b.mjs`, source)
  const unreferenced = write(`${base}support/unused.js`, source)
  const scanned = scanAll(join(root, 'requirements'))
  for (const file of [referenced, unreferenced]) {
    assert.ok(scanned[relative(process.cwd(), file)].some((hit) => hit.rule === 'deep-dist-import'))
  }
})
