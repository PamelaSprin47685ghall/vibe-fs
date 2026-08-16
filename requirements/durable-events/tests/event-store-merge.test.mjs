// DURABLE-EVENTS-003/014: the only merge primitive is EventKWayMerge over writer streams.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const envelope = (id, parents = [], stream = 'proof/merge', payload = {}) => ({
  id,
  stream,
  type: 'JobRequested',
  parents,
  payload,
  payloadRefs: [],
})

test('WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_writer_enumeration_independent', () => {
  const a = envelope('0'.repeat(39) + 'a')
  const b = envelope('0'.repeat(39) + 'b')
  const c = envelope('0'.repeat(39) + 'c', ['0'.repeat(39) + 'a'])

  const left = eventStore.merge([
    ['writer-a', [a, c]],
    ['writer-b', [b]],
  ])
  const right = eventStore.merge([
    ['writer-b', [b]],
    ['writer-a', [a, c]],
  ])

  assert.equal(left.ok, true)
  assert.equal(right.ok, true)
  assert.deepEqual(left.events.map((e) => e.id), right.events.map((e) => e.id))
})

test('WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_same_bytes_dedupes', () => {
  const id = '0'.repeat(39) + '1'
  const sameA = envelope(id, [], 'proof/a', { x: 1 })
  const sameB = envelope(id, [], 'proof/a', { x: 1 })

  const merged = eventStore.merge([
    ['a', [sameA]],
    ['b', [sameB]],
  ])
  assert.equal(merged.ok, true)
  assert.equal(merged.events.length, 1)
})

test('WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_different_bytes_fail_closed', () => {
  const id = '0'.repeat(39) + '2'
  const a = envelope(id, [], 'proof/a', { x: 1 })
  const b = envelope(id, [], 'proof/a', { x: 2 })

  const result = eventStore.merge([
    ['a', [a]],
    ['b', [b]],
  ])
  assert.equal(result.ok, false)
})

test('WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_missing_parent_fails_closed', () => {
  const child = envelope('0'.repeat(39) + '3', ['0'.repeat(39) + '9'])
  const result = eventStore.merge([['a', [child]]])
  assert.equal(result.ok, false)
})
