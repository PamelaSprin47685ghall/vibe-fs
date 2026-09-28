import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { validateModuleLinkage, validateModuleLoadability, run } from '../../../scripts/checks/js-module-linkage.mjs'
import { createWorkspaceFixture } from './support/workspace-fixture.mjs'
import { HOST_PHYSICAL_CANARY_FILES, scanFile } from '../../../scripts/lib/test-surface-scan.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[js-semantic-surface-006] non-compiler physical probes do not import unregistered internal modules', {
  todo: '03-D2: existing Host exit probes bypass the public surface; retain their physical proof pending the boundary decision',
}, () => {
  const violations = [...HOST_PHYSICAL_CANARY_FILES]
    .filter((file) => existsSync(join(ROOT, file)))
    .flatMap((file) => scanFile(join(ROOT, file), file))
    .filter((hit) => hit.rule === 'deep-dist-import')
  assert.deepEqual(violations, [])
})

test('WHAT[js-semantic-surface-006] the complete emitted graph links and its production modules load', async () => {
  assert.deepEqual(validateModuleLinkage(join(ROOT, 'dist')), [])
  assert.deepEqual(await validateModuleLoadability(join(ROOT, 'dist')), [])
})

test('WHAT[js-semantic-surface-006] a closed graph supports named, default, namespace and re-exported values', async (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('dist/value.js', 'export const value = 1; export default 2\n')
  write('dist/barrel.js', "export * from './value.js'; export * as api from './value.js'\n")
  write('dist/consumer.js', "import value from './value.js'; import { value as named, api } from './barrel.js'; export const result = value + named + api.value\n")
  assert.deepEqual(validateModuleLinkage(join(root, 'dist')), [])
  assert.equal(await run({ root }), 0)
})

test('WHAT[js-semantic-surface-006] missing modules, missing exports and invalid emitted syntax fail', async (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('dist/value.js', 'export const visible = 1\n')
  write('dist/consumer.js', "import { missing } from './value.js'\n")
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /missing named export 'missing'/)
  write('dist/consumer.js', "import './absent.js'\n")
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /missing emitted module/)
  write('dist/consumer.js', 'export const =\n')
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /invalid emitted ESM/)
  assert.equal(await run({ root }), 1)
})

test('WHAT[js-semantic-surface-006] static imports, re-exports and deferred literal imports cannot escape the package', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('src/outside.js', 'export const value = 1\n')
  for (const source of [
    "import { value } from '../src/outside.js'",
    "export { value } from '../src/outside.js'",
    "export * from '../src/outside.js'",
    "export const load = () => import('../src/outside.js')",
  ]) {
    write('dist/consumer.js', source)
    assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /escapes dist package closure/, source)
  }
})

test('WHAT[js-semantic-surface-006] uncalled literal imports and re-exported names are still linked', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('dist/value.js', 'export const visible = 1\n')
  write('dist/consumer.js', "export const load = () => import('./absent.js')\n")
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /missing emitted module/)
  write('dist/consumer.js', "export { missing as publicValue } from './value.js'\n")
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /missing named export 'missing'/)
  write('dist/consumer.js', "export const load = () => import('./value.js')\n")
  assert.deepEqual(validateModuleLinkage(join(root, 'dist')), [])
})

test('WHAT[js-semantic-surface-006] linking uses the actual ESM path without inventing a file extension', (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('dist/value.js', 'export const value = 1\n')
  write('dist/consumer.js', "import { value } from './value'\n")
  assert.match(validateModuleLinkage(join(root, 'dist')).join('\n'), /missing emitted module/)
})

test('WHAT[js-semantic-surface-006] loadability failure blocks the gate even when static linkage succeeds', async (t) => {
  const { root, write } = createWorkspaceFixture(t)
  write('dist/broken.js', "throw new Error('initialization failed')\n")
  assert.deepEqual(validateModuleLinkage(join(root, 'dist')), [])
  assert.match((await validateModuleLoadability(join(root, 'dist'))).join('\n'), /initialization failed/)
  assert.equal(await run({ root }), 1)
})
