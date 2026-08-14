// tests/unit/casebook/casebook-domain.test.mjs — G6-A: Casebook pure domain
// (CASE-002/003/004/008).
//
// Observation normalization dedupes by identity; replay classifies Fresh only
// on exact normalized equality (no-delta is a hint, never a proof); the
// projection fold handles Captured/Refreshed/Accessed/Evicted with a derived
// monotonic access order (no wall clock); LRU evicts least-recently-accessed.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  Observation,
  Observations_normalize as normalize,
  Observations_classifyReplay as classifyReplay,
  CasebookEvent,
  CasebookProjection_fold as fold,
  CasebookProjection_evict as evict,
} from '../../../dist/Domain/Casebook.js'
import { caseOf, listItems, mapEntries, toList } from '../../verification-system/tests/support/domain.mjs'

const caseIndex = (cls, name) => Object.create(cls.prototype).cases().indexOf(name)
const observation = (name, payload) => new Observation(caseIndex(Observation, name), payload)
const read = (path, hash) => observation('FileRead', [path, hash])
const glob = (pattern, paths) => observation('GlobResult', [pattern, toList(paths)])

const event = (name, payload) => new CasebookEvent(caseIndex(CasebookEvent, name), payload)
const captured = (sessionId, q, a, observations) =>
  event('CaseCaptured', [{ SessionId: sessionId, Q: q, A: a, Observations: toList(observations), LastAccessOrder: 0 }])
const refreshed = (sessionId, q, a, observations) => event('CaseRefreshed', [sessionId, q, a, toList(observations)])
const accessed = (sessionId) => event('CaseAccessed', [sessionId])
const evicted = (sessionId) => event('CaseEvicted', [sessionId])

test('CASE003_normalize_dedupes_and_orders_observations', () => {
  const obs = [read('a.txt', 'h1'), read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y']), glob('**/*.fs', ['y', 'x'])]
  const normalized = listItems(normalize(toList(obs)))
  // same identity → one entry; glob paths order-insensitive
  assert.equal(normalized.length, 2)
})

test('CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const stored = [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y'])]
  // exact replay (order-insensitive glob) → Fresh
  assert.equal(caseOf(classifyReplay(toList(stored), toList([glob('**/*.fs', ['y', 'x']), read('a.txt', 'h1')]))), 'Fresh')
  // content changed → Stale
  assert.equal(caseOf(classifyReplay(toList(stored), toList([read('a.txt', 'h2'), glob('**/*.fs', ['x', 'y'])]))), 'Stale')
  // file deleted → Stale
  assert.equal(caseOf(classifyReplay(toList(stored), toList([glob('**/*.fs', ['x', 'y'])]))), 'Stale')
  // extra result → Stale
  assert.equal(caseOf(classifyReplay(toList(stored), toList([read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y', 'z'])]))), 'Stale')
})

test('CASE002_008_fold_captured_refreshed_accessed_evicted', () => {
  const cases = fold(
    toList([
      captured('s1', 'Q1', 'A1', [read('a.txt', 'h1')]),
      captured('s2', 'Q2', 'A2', [read('b.txt', 'h2')]),
      refreshed('s1', 'Q1b', 'A1b', [read('a.txt', 'h1'), read('c.txt', 'h3')]),
      accessed('s2'),
    ]),
  )
  assert.equal(mapEntries(cases).length, 2)
  const s1 = mapEntries(cases).find(([k]) => k === 's1')[1]
  assert.equal(s1.A, 'A1b')
  assert.equal(listItems(s1.Observations).length, 2)
  // Evicted removes the Case (captured+evicted in one fold)
  const combined = fold(toList([captured('s2', 'Q2', 'A2', []), evicted('s2')]))
  assert.equal(mapEntries(combined).length, 0)
})

test('CASE008_lru_evict_keeps_most_recently_accessed', () => {
  const cases = fold(
    toList([captured('s1', 'Q1', 'A1', []), captured('s2', 'Q2', 'A2', []), captured('s3', 'Q3', 'A3', []), accessed('s1')]),
  )
  const [kept, victims] = evict(2, cases)
  // s2 was accessed first (order 1), s3 second (2), s1 last (3) → evict s2
  assert.deepEqual(listItems(victims), ['s2'])
  assert.deepEqual(mapEntries(kept).map(([k]) => k).sort(), ['s1', 's3'])
  // capacity >= count → no eviction
  const [keptAll, none] = evict(3, cases)
  assert.deepEqual(listItems(none), [])
  assert.equal(mapEntries(keptAll).length, 3)
})
