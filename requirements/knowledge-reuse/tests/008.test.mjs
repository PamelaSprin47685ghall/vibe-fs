import assert from 'node:assert/strict'
import test from 'node:test'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'
import * as wiring from '../../../dist/OpenCode/Host/KnowledgeLifecycleSurface.js'

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_fold_accessed_and_evicted_derives_access_order', () => {
  let entries = [
    domain.createCase('c1', 'q1', 'a1', [], '2026-01-01T00:00:00Z'),
    domain.createCase('c2', 'q2', 'a2', [], '2026-01-01T00:00:00Z'),
  ]
  entries = domain.recordAccess('c1', '2026-01-02T00:00:00Z', entries)
  assert.deepEqual(domain.accessOrder(entries), ['c2', 'c1'])
})

test('WHAT[KNOWLEDGE-REUSE-008] CASE008_lru_evict_keeps_most_recently_accessed', () => {
  let entries = [
    domain.createCase('c1', 'q1', 'a1', [], '2026-01-01T00:00:00Z'),
    domain.createCase('c2', 'q2', 'a2', [], '2026-01-01T00:00:00Z'),
  ]
  entries = domain.recordAccess('c1', '2026-01-02T00:00:00Z', entries)
  const afterEvict = domain.evictLru(1, entries)
  assert.deepEqual(afterEvict.map((e) => e.id), ['c1'])
})

test('WHAT[KNOWLEDGE-REUSE-008] lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order', async () => {
  const order = await wiring.testAccessOrdering(['c1', 'c2'], 'c1')
  assert.equal(order[order.length - 1], 'c1')
})
