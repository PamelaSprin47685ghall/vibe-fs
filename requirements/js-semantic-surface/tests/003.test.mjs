import assert from 'node:assert/strict'
import { rmSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { run, validateSurfaceManifest } from '../../../scripts/checks/js-surface-manifest.mjs'
import { SURFACE_MANIFEST } from '../../../scripts/lib/test-surface-scan.mjs'
import { createManifestFixture } from './support/manifest-fixture.mjs'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))

test('WHAT[js-semantic-surface-003] current registrations are emitted and compiled', () => {
  assert.ok(SURFACE_MANIFEST.length > 0)
  assert.deepEqual(validateSurfaceManifest(SURFACE_MANIFEST, ROOT), [])
})

test('WHAT[js-semantic-surface-003] uncompiled and un-emitted sources cannot satisfy registration', (t) => {
  const { root, entry, write } = createManifestFixture(t)
  write('src/Wanxiangshu/Wanxiangshu.Owner.owner.fsproj', '<Project/>')
  assert.match(validateSurfaceManifest([entry], root).join('\n'), /not compiled/)
  write('src/Wanxiangshu/Wanxiangshu.Owner.owner.fsproj', '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
  rmSync(join(root, 'dist', entry.module))
  assert.match(validateSurfaceManifest([entry], root).join('\n'), /missing emitted surface/)
})

test('WHAT[js-semantic-surface-003] malformed entries and duplicate modules fail the same registration gate', (t) => {
  const { root, entry, write } = createManifestFixture(t)
  for (const [field, invalid, expected] of [
    ['module', 'dist://escape.js', /relative emitted \.js path/],
    ['source', 'other/Missing.fs', /source must be a src\/Wanxiangshu \.fs path/],
  ]) assert.match(validateSurfaceManifest([{ ...entry, [field]: invalid }], root).join('\n'), expected, field)
  assert.match(validateSurfaceManifest([entry, entry], root).join('\n'), /duplicate manifest module/)
  assert.match(validateSurfaceManifest('not an array', root).join('\n'), /must be an array/)
  assert.match(validateSurfaceManifest([null], root).join('\n'), /must be an object/)
  assert.deepEqual(validateSurfaceManifest([entry], root), [])
  assert.equal(run({ root, manifest: [{ ...entry, module: 'dist://escape.js' }] }), 1)
  assert.equal(run({ root, manifest: [entry] }), 0)
  write(entry.source, 'module Owner.Surface\nlet value = 1\n')
})
