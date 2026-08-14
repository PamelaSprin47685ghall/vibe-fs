// FROZEN — 2026-08-14. Historical filename retained; snapshot merge is gone.
// Intentionally NOT executed before implementation.
// DURABLE-EVENTS-003/014: the only merge primitive is EventKWayMerge over writer streams.

import assert from 'node:assert/strict'
import test from 'node:test'
import { eventId, listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const Merge = await import('../../../dist/Persistence/EventStore/EventKWayMerge.js')
const streamId = (value) => Domain.EventStreamIdModule_create(value)
const envelope = (id, parents = [], stream = 'proof/merge', payload = {}) => new Domain.EventEnvelope(
  eventId(id), streamId(stream), 'JobRequested', toList(parents.map(eventId)), payload, toList([]),
)
const unwrap = (value) => {
  const r = resultOf(value)
  assert.equal(r.ok, true, `expected Ok, got ${JSON.stringify(r.error)}`)
  return listItems(r.value)
}

test('DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent', () => {
  const a = envelope('a'.padStart(40, '0'))
  const b = envelope('b'.padStart(40, '0'))
  const c = envelope('c'.padStart(40, '0'), ['a'.padStart(40, '0')])
  const left = unwrap(Merge.merge(toList([['writer-a', toList([a, c])], ['writer-b', toList([b])]])))
  const right = unwrap(Merge.merge(toList([['writer-b', toList([b])], ['writer-a', toList([a, c])]])))
  assert.deepEqual(left.map((e) => e.EventId.fields?.[0] ?? e.EventId), right.map((e) => e.EventId.fields?.[0] ?? e.EventId))
})

test('DURABLE_EVENTS_003_same_EventId_same_bytes_dedupes', () => {
  const id = '1'.padStart(40, '0')
  const sameA = envelope(id, [], 'proof/a', { x: 1 })
  const sameB = envelope(id, [], 'proof/a', { x: 1 })
  const merged = unwrap(Merge.merge(toList([['a', toList([sameA])], ['b', toList([sameB])]])))
  assert.equal(merged.length, 1)
})

test('DURABLE_EVENTS_003_same_EventId_different_bytes_fail_closed', () => {
  const id = '2'.padStart(40, '0')
  const a = envelope(id, [], 'proof/a', { x: 1 })
  const b = envelope(id, [], 'proof/a', { x: 2 })
  const r = resultOf(Merge.merge(toList([['a', toList([a])], ['b', toList([b])]])))
  assert.equal(r.ok, false)
})

test('DURABLE_EVENTS_014_missing_parent_fails_closed', () => {
  const child = envelope('3'.padStart(40, '0'), ['9'.padStart(40, '0')])
  const r = resultOf(Merge.merge(toList([['a', toList([child])]])))
  assert.equal(r.ok, false)
})
