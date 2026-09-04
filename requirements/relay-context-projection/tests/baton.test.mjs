import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'

test('WHAT[PROJ-004] first baton is ExistingWorld without invented predecessor facts', () => {
  const baton = projection.baton({
    roadId: 'road-1',
    fromIncumbency: null,
    source: 'ExistingWorld',
    authorityRevision: 'authority-1',
    snapshotId: 'snapshot-1',
    risks: [],
    evidenceRefs: [],
  })
  assert.equal(baton.source, 'ExistingWorld')
  assert.equal(baton.fromIncumbency, null)
  assert.equal(/reviewer|perfect|previous commit/i.test(baton.canonical), false)
})

test('WHAT[PROJ-006] baton canonicalization is deterministic bounded and strips secret-like fields', () => {
  const input = {
    roadId: 'road-1',
    fromIncumbency: 'inc-1',
    source: 'Retirement',
    authorityRevision: 'authority-1',
    snapshotId: 'snapshot-1',
    risks: Array.from({ length: 40 }, (_, i) => `risk-${i}`),
    evidenceRefs: Array.from({ length: 40 }, (_, i) => `evidence-${i}`),
    secret: 'never-copy-me',
    reasoning: 'hidden',
  }
  const first = projection.baton(input)
  const second = projection.baton({ ...input, evidenceRefs: input.evidenceRefs.slice() })
  assert.equal(first.canonical, second.canonical)
  assert.equal(first.digest, second.digest)
  assert.ok(first.risks.length <= projection.maxRisks)
  assert.ok(first.evidenceRefs.length <= projection.maxEvidenceRefs)
  assert.equal(first.canonical.includes('never-copy-me'), false)
  assert.equal(first.canonical.includes('hidden'), false)
})
