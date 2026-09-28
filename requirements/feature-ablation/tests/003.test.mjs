import assert from 'node:assert/strict'
import test from 'node:test'
import { withAblationFixture, withAblationEnv } from './support/ablation-fixture.mjs'

test('WHAT[feature-ablation-003] loading rejects cycles through either edge kind', async () => {
  for (const kind of ['station-order', 'borrow']) {
    await withAblationFixture((doc) => {
      doc.edges.push({ from: 'verification-system', to: 'requirement-system', kind })
    }, (surface) => {
      const result = surface.load()
      assert.equal(result.ok, false)
      assert.equal(result.kind, 'DagViolation')
      assert.match(result.error, /cycle/i)
      assert.deepEqual(surface.manifestNodeIds(), [])
    })
  }
})

test('WHAT[feature-ablation-003] station-order and borrow prerequisites are enforced on load', async () => {
  for (const kind of ['station-order', 'borrow']) {
    await withAblationFixture((doc) => {
      doc.nodes = ['prerequisite', 'target'].map((id) => ({ id, package: id,
        station: id === 'prerequisite' ? 0 : 1, kind: 'package', borrowed_surface: [] }))
      doc.edges = [{ from: 'prerequisite', to: 'target', kind }]
    }, (surface) => {
      for (const from of ['ablated', 'borrowed', 'active']) {
        for (const to of ['ablated', 'borrowed', 'active']) {
          withAblationEnv([
            ['WANXIANGSHU_ABLATION_prerequisite', from],
            ['WANXIANGSHU_ABLATION_target', to],
          ], () => {
            const result = surface.load()
            const allowed = from !== 'ablated' || (kind === 'station-order' ? to !== 'active' : to === 'ablated')
            assert.equal(result.ok, allowed, `${kind}: ${from} -> ${to}`)
            if (!allowed) assert.equal(result.kind, 'DagViolation')
          })
        }
      }
    })
  }
})
