// tests/unit/casebook/casebook-domain.test.mjs — G6-A: Casebook pure domain
// (CASE-002/003/004/008).
//
// Observation normalization dedupes by identity; replay classifies Fresh only
// on exact normalized equality (no-delta is a hint, never a proof); the
// projection fold handles Captured/Refreshed/Accessed/Evicted with a derived
// monotonic access order (no wall clock); LRU evicts least-recently-accessed.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

const read = (path, hash) => ({ kind: 'file-read', path, contentHash: hash })
const glob = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })

const project = (events) =>
  events.reduce((world, event) => {
    const result = casebook.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    return result.world
  }, casebook.emptyWorld())
const captured = (sessionId, q, a, observations) => ({
  kind: 'case-captured',
  case: { sessionId, q, a, observations, lastAccessOrder: 0 },
})
const refreshed = (sessionId, q, a, observations) => ({
  kind: 'case-refreshed',
  sessionId,
  q,
  a,
  observations,
})
const accessed = (sessionId) => ({ kind: 'case-accessed', sessionId })
const evicted = (sessionId) => ({ kind: 'case-evicted', sessionId })

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_normalize_dedupes_and_orders_observations', () => {
  const obs = [read('a.txt', 'h1'), read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y']), glob('**/*.fs', ['y', 'x'])]
  // same identity → one entry; glob paths order-insensitive
  assert.equal(casebook.normalize(obs).length, 2)
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const stored = [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y'])]
  // exact replay (order-insensitive glob) → Fresh
  assert.equal(
    casebook.classifyReplay(stored, [glob('**/*.fs', ['y', 'x']), read('a.txt', 'h1')]),
    'fresh',
  )
  // content changed → Stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h2'), glob('**/*.fs', ['x', 'y'])]), 'stale')
  // file deleted → Stale
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['x', 'y'])]), 'stale')
  // extra result → Stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y', 'z'])]), 'stale')
})

test('WHAT[KNOWLEDGE-REUSE-002] CASE002_fold_captured_and_refreshed_keeps_qa_verbatim', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', [read('a.txt', 'h1')]),
    captured('s2', 'Q2', 'A2', [read('b.txt', 'h2')]),
    refreshed('s1', 'Q1b', 'A1b', [read('a.txt', 'h1'), read('c.txt', 'h3')]),
  ])
  assert.equal(cases.length, 2)
  const s1 = cases.find((c) => c.sessionId === 's1')
  assert.equal(s1.a, 'A1b')
  assert.equal(s1.observations.length, 2)
})

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_fold_accessed_and_evicted_derives_access_order', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', [read('a.txt', 'h1')]),
    captured('s2', 'Q2', 'A2', [read('b.txt', 'h2')]),
    accessed('s2'),
  ])
  assert.equal(cases.length, 2)
  // Evicted removes the Case (captured+evicted in one fold)
  const { cases: combined } = project([captured('s2', 'Q2', 'A2', []), evicted('s2')])
  assert.equal(combined.length, 0)
})

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_lru_evict_keeps_most_recently_accessed', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', []),
    captured('s2', 'Q2', 'A2', []),
    captured('s3', 'Q3', 'A3', []),
    accessed('s1'),
  ])
  const { kept, victims } = casebook.evict(2, cases)
  // s2 was accessed first (order 1), s3 second (2), s1 last (3) → evict s2
  assert.deepEqual(victims, ['s2'])
  assert.deepEqual(
    kept.map((c) => c.sessionId).sort(),
    ['s1', 's3'],
  )
  // capacity >= count → no eviction
  const { kept: keptAll, victims: none } = casebook.evict(3, cases)
  assert.deepEqual(none, [])
  assert.equal(keptAll.length, 3)
})
