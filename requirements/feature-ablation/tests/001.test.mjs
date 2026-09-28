import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { withAblationFixture } from './support/ablation-fixture.mjs'

const root = fileURLToPath(new URL('../../..', import.meta.url))
const nodes = JSON.parse(readFileSync(join(root, 'resources/ablation/nodes.json'), 'utf8')).nodes

test('WHAT[feature-ablation-001] every package has exactly one same-name primary node', () => {
  const packages = readdirSync(join(root, 'requirements'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name !== 'proposals')
    .map((entry) => entry.name).sort()
  const primary = nodes.filter((node) => node.kind === 'package')
  assert.deepEqual(primary.map((node) => node.id).sort(), packages)
  for (const node of primary) assert.equal(node.package, node.id)
  for (const node of nodes.filter((entry) => entry.kind === 'slice')) {
    const parent = primary.find((entry) => entry.id === node.parent)
    assert.ok(parent, `missing parent of ${node.id}`)
    assert.equal(node.package, parent.package)
  }
})

test('WHAT[feature-ablation-001] the real loader accepts a valid registry in an isolated package', async () => {
  await withAblationFixture(() => {}, (surface) => {
    assert.equal(surface.load().ok, true)
    assert.deepEqual(surface.manifestNodeIds().sort(), nodes.map((node) => node.id).sort())
  })
})

test('WHAT[feature-ablation-001] the real loader rejects missing and duplicate primary nodes', async () => {
  for (const mutate of [
    (doc) => { doc.nodes.find((node) => node.id === 'feature-ablation').kind = 'custom' },
    (doc) => { doc.nodes.push({ ...doc.nodes.find((node) => node.id === 'feature-ablation'), id: 'extra-primary' }) },
  ]) {
    await withAblationFixture(mutate, (surface) => {
      const result = surface.load()
      assert.equal(result.ok, false)
      assert.equal(result.kind, 'InvalidManifest')
      assert.deepEqual(surface.manifestNodeIds(), [])
    })
  }
})

test('WHAT[feature-ablation-001] child nodes must name an existing parent in their own package', async () => {
  for (const parent of [undefined, 'missing-parent', 'requirement-system']) {
    await withAblationFixture((doc) => {
      doc.nodes.push({ id: 'feature-ablation.example', package: 'feature-ablation', station: 0,
        kind: 'slice', parent, borrowed_surface: [] })
    }, (surface) => {
      const result = surface.load()
      assert.equal(result.ok, false)
      assert.equal(result.kind, 'InvalidManifest')
    })
  }
})
