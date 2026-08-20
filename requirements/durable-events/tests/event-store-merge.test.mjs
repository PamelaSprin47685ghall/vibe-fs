// DURABLE-EVENTS-003/014: k-way ordering belongs to the merge owner.

import assert from 'node:assert/strict'
import { performance } from 'node:perf_hooks'
import test from 'node:test'

import * as eventMerge from '../../../dist/Persistence/EventStore/MergeSurface.js'

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

  const left = eventMerge.merge([
    ['writer-a', [a, c]],
    ['writer-b', [b]],
  ])
  const right = eventMerge.merge([
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

  const merged = eventMerge.merge([
    ['a', [sameA]],
    ['b', [sameB]],
  ])
  assert.equal(merged.ok, true)
  assert.equal(merged.events.length, 1)
  assert.deepEqual(merged.events[0].payload, { x: 1 }, 'merge returns the event payload, not the canonical envelope')
})

test('WHAT[DURABLE-EVENTS-003] DURABLE_EVENTS_003_same_EventId_different_bytes_fail_closed', () => {
  const id = '0'.repeat(39) + '2'
  const a = envelope(id, [], 'proof/a', { x: 1 })
  const b = envelope(id, [], 'proof/a', { x: 2 })

  const result = eventMerge.merge([
    ['a', [a]],
    ['b', [b]],
  ])
  assert.equal(result.ok, false)
})

test('WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_missing_parent_fails_closed', () => {
  const child = envelope('0'.repeat(39) + '3', ['0'.repeat(39) + '9'])
  const result = eventMerge.merge([['a', [child]]])
  assert.equal(result.ok, false)
})

test('WHAT[DURABLE-EVENTS-014] k-way merge does not re-sort every writer head for every event', () => {
  const writers = 512
  const eventsPerWriter = 16
  let nextId = 0
  const streams = Array.from({ length: writers }, (_, writer) => [
    `writer-${String(writer).padStart(4, '0')}`,
    Array.from({ length: eventsPerWriter }, () => envelope((nextId++).toString(16).padStart(40, '0'))),
  ])

  const started = performance.now()
  const merged = eventMerge.merge(streams)
  const elapsedMs = performance.now() - started

  assert.equal(merged.ok, true)
  assert.equal(merged.events.length, writers * eventsPerWriter)
  assert.ok(elapsedMs < 500, `8192-event / 512-writer merge took ${elapsedMs.toFixed(1)}ms`)
})
