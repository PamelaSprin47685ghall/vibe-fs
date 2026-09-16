// requirements/knowledge-reuse/tests/casebook-surface.test.mjs
//
// CasebookSurface contract test (PR 4/PR 7): the registered semantic surface
// is the legal JS entry point (JS-SEMANTIC-SURFACE-002/003/005). A JS test
// speaks observation/event vocabulary in plain JS and reads JS-shaped
// answers; the F# Observation/CasebookEvent/Case unions never cross.
//
// Laws exercised: KNOWLEDGE-REUSE-002 (Q/A verbatim), 003 (typed capture +
// normalization), 004 (replay classification), 008 (LRU eviction),
// 010 (exactly-once finalize).

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

const read = (path, hash) => ({ kind: 'file-read', path, contentHash: hash })
const glob = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })
const caseRec = (sessionId, q, a, observations) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})
const project = (events) =>
  events.reduce((world, event) => {
    const result = casebook.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    return result.world
  }, casebook.emptyWorld())

const openStore = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-surface-'))
  return {
    dir,
    handle: eventStore.create(dir, 'casebook-surface'),
    close() {
      eventStore.dispose(this.handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

test('WHAT[KNOWLEDGE-REUSE-003] CASE003_normalize_dedupes_and_orders_observations', () => {
  const obs = [read('a.txt', 'h1'), read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y']), glob('**/*.fs', ['y', 'x'])]
  // same identity → one entry; glob paths order-insensitive
  assert.equal(casebook.normalize(obs).length, 2)
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const stored = [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y'])]
  // exact replay (order-insensitive glob) → fresh
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['y', 'x']), read('a.txt', 'h1')]), 'fresh')
  // content changed → stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h2'), glob('**/*.fs', ['x', 'y'])]), 'stale')
  // file deleted → stale
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['x', 'y'])]), 'stale')
  // extra result → stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y', 'z'])]), 'stale')
})

test('WHAT[KNOWLEDGE-REUSE-002] CASE002_fold_captured_and_refreshed_keeps_qa_verbatim', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', [read('a.txt', 'h1')]) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', [read('b.txt', 'h2')]) },
    { kind: 'case-refreshed', sessionId: 's1', q: 'Q1b', a: 'A1b', observations: [read('a.txt', 'h1'), read('c.txt', 'h3')] },
  ])
  assert.equal(folded.cases.length, 2)
  const s1 = folded.cases.find((c) => c.sessionId === 's1')
  assert.equal(s1.a, 'A1b')
  assert.equal(s1.observations.length, 2)
})

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_fold_accessed_and_evicted_derives_access_order', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', [read('a.txt', 'h1')]) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', [read('b.txt', 'h2')]) },
    { kind: 'case-accessed', sessionId: 's2' },
  ])
  assert.equal(folded.cases.length, 2)
  // Evicted removes the Case (captured+evicted in one fold)
  const combined = project([
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', []) },
    { kind: 'case-evicted', sessionId: 's2' },
  ])
  assert.equal(combined.cases.length, 0)
})

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_lru_evict_keeps_most_recently_accessed', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', []) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', []) },
    { kind: 'case-captured', case: caseRec('s3', 'Q3', 'A3', []) },
    { kind: 'case-accessed', sessionId: 's1' },
  ])
  const { kept, victims } = casebook.evict(2, folded.cases)
  // s2 was accessed first (order 1), s3 second (2), s1 last (3) → evict s2
  assert.deepEqual(victims, ['s2'])
  assert.deepEqual(kept.map((c) => c.sessionId).sort(), ['s1', 's3'])
  // capacity >= count → no eviction
  const keptAll = casebook.evict(3, folded.cases)
  assert.deepEqual(keptAll.victims, [])
  assert.equal(keptAll.kept.length, 3)
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_is_exactly_once_per_scope', async () => {
  const local = openStore()
  try {
    const first = await casebook.finalize(local.handle, caseRec('scope-1', 'Q', 'A', []))
    assert.equal(first.ok, true, JSON.stringify(first.error))
    const second = await casebook.finalize(local.handle, caseRec('scope-1', 'Q', 'A2', []))
    assert.equal(second.ok, false, 'a second finalize for the same scope must be refused')
    assert.match(second.error, /already finalized/)
    const other = await casebook.finalize(local.handle, caseRec('scope-2', 'Q', 'A', []))
    assert.equal(other.ok, true, JSON.stringify(other.error))
  } finally {
    local.close()
  }
})
