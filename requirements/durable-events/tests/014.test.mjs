import assert from 'node:assert/strict'
import test from 'node:test'
import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'

test('WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent', () => {
  const w1 = [{ eventId: 'e1', timestamp: 100 }, { eventId: 'e3', timestamp: 300 }]
  const w2 = [{ eventId: 'e2', timestamp: 200 }]
  const res1 = merge.kWayMerge([w1, w2])
  const res2 = merge.kWayMerge([w2, w1])
  assert.deepEqual(res1.map((e) => e.eventId), ['e1', 'e2', 'e3'])
  assert.deepEqual(res2.map((e) => e.eventId), ['e1', 'e2', 'e3'])
})

test('WHAT[DURABLE-EVENTS-014] k-way merge does not re-sort every writer head for every event', () => {
  const stats = merge.mergeStats([[{ eventId: 'e1', timestamp: 1 }], [{ eventId: 'e2', timestamp: 2 }]])
  assert.ok(stats.comparisons <= 5)
})
