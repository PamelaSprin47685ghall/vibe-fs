import assert from 'node:assert/strict'
import { join } from 'node:path'
import test from 'node:test'
import { run as runBoundaryGate } from '../../../scripts/checks/js-boundary-gate.mjs'
import { scanAll, SURFACE_MANIFEST } from '../../../scripts/lib/test-surface-scan.mjs'
import { createWorkspaceFixture } from './support/workspace-fixture.mjs'

const importSource = (module) => `import * as api from '../../../../dist/${module}'\n`
const hits = (root) => Object.values(scanAll(join(root, 'requirements'))).flat()

test('WHAT[js-semantic-surface-002] the current semantic zone passes the implemented boundary gate', () => {
  assert.equal(runBoundaryGate(), 0)
})

test('WHAT[js-semantic-surface-002] the same gate accepts a registered public import and rejects an internal module', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  const file = 'requirements/consumer/tests/support/api.mjs'
  write(file, importSource(SURFACE_MANIFEST[0].module))
  assert.equal(runBoundaryGate({ root }), 0)
  write(file, importSource('Internal/Unregistered.js'))
  assert.equal(runBoundaryGate({ root }), 1)
  assert.ok(hits(root).some((hit) => hit.rule === 'deep-dist-import'))
})

test('WHAT[js-semantic-surface-002] module discovery, generated-name probing and runtime representation access are detected', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  const source = [
    importSource(SURFACE_MANIFEST[0].module),
    ['Object.', 'entries(api)'].join(''),
    `const prefix = key => key.startsWith('${['Foo', '__'].join('')}')`,
    `const suffix = key => key.endsWith('${['_', 'Bar'].join('')}')`,
    ['const payload = value => value.', 'fields[0]'].join(''),
    ['import { list } from "../../../dist/fable', '_modules/List.js"'].join(''),
  ].join('\n')
  write('requirements/probe/tests/001.test.mjs', source)
  const rules = hits(root).map((hit) => hit.rule)
  for (const rule of ['export-discovery', 'mangled-lookup', 'du-shape', 'fable-modules']) {
    assert.ok(rules.includes(rule), `missing detection: ${rule}`)
  }
  assert.equal(rules.filter((rule) => rule === 'mangled-lookup').length, 2)
})

test('WHAT[js-semantic-surface-002] a template that chooses an unregistered compiled module is detected', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  const source = ['const load = modulePath => import(new URL(`../../../', 'dist',
    '/${modulePath}.js`, import.meta.url))'].join('')
  write('requirements/probe/tests/001.test.mjs', source)
  assert.ok(hits(root).some((hit) => hit.rule === 'template-dist-import'))
})

test('WHAT[js-semantic-surface-002] ordinary data enumeration and standard function binding remain legal', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('requirements/probe/tests/001.test.mjs', [
    'const data = { value: 1 }',
    'const entries = Object.entries(data)',
    'const call = (() => entries).bind(null)',
    'export { call }',
  ].join('\n'))
  assert.deepEqual(hits(root), [])
})
