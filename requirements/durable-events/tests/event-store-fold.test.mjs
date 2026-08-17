// Structural Current is owned by EventStore; history ordering is owned by merge.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as eventMerge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const A = 'a'.repeat(40)
const B = 'b'.repeat(40)
const C = 'c'.repeat(40)
const D = 'd'.repeat(40)

const make = ({
  id,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) => ({
  id,
  stream,
  type: eventType,
  parents,
  payload,
  payloadRefs,
})

const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-event-store-fold-'))
  return fn(base)
}

test('WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_k_way_merge_rejects_missing_parent_fail_closed', () => {
  const child = make({ id: B, parents: [A] })
  const result = eventMerge.merge([['writer', [child]]])
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'MissingParent')
  assert.equal(result.error.eventId, A)
})

test('WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_014_k_way_merge_rejects_backward_or_cyclic_writer_frontier', () => {
  const a = make({ id: A, parents: [B] })
  const b = make({ id: B, parents: [A] })
  const result = eventMerge.merge([['writer', [a, b]]])
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'NonCanonical')
})

test('WHAT[DURABLE-EVENTS-014] DURABLE_EVENTS_014_k_way_merge_is_deterministic_with_EventId_tiebreak', () => {
  const root = make({ id: A })
  const high = make({ id: C, parents: [A], eventType: 'JobAccepted' })
  const low = make({ id: B, parents: [A], eventType: 'JobRejected' })

  const first = eventMerge.merge([
    ['writer-a', [root, high]],
    ['writer-b', [low]],
  ])
  const second = eventMerge.merge([
    ['writer-b', [low]],
    ['writer-a', [root, high]],
  ])

  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.deepEqual(first.events.map((e) => e.id), [A, B, C])
  assert.deepEqual(second.events.map((e) => e.id), first.events.map((e) => e.id))
})

test('WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'conflict-proof')
  try {
    assert.equal((await eventStore.append(store, [make({ id: A, stream: 'job/conflict' })])).ok, true)
    assert.equal((await eventStore.append(store, [make({ id: B, stream: 'job/conflict' })])).ok, true)

    assert.deepEqual(eventStore.heads(store, 'job/conflict').sort(), [A, B])
    assert.equal(eventStore.head(store, 'job/conflict'), null, 'a fork must not masquerade as a unique head')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'resolution-proof')
  try {
    assert.equal((await eventStore.append(store, [make({ id: A, stream: 'job/resolution' })])).ok, true)
    assert.equal((await eventStore.append(store, [make({ id: B, stream: 'job/resolution' })])).ok, true)
    assert.equal(
      (await eventStore.append(store, [make({ id: D, stream: 'job/resolution', eventType: 'JobConflictResolved', parents: [A, B] })])).ok,
      true,
    )

    assert.deepEqual(eventStore.heads(store, 'job/resolution'), [D])
    assert.equal(eventStore.head(store, 'job/resolution'), D)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-007] DURABLE_EVENTS_007_unknown_authoritative_event_type_is_rejected_before_durability', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'unknown-type-fold')
  try {
    const result = await eventStore.append(store, [make({ id: A, eventType: 'TotallyUnknownEventType' })])
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'StorageInvalid')
    assert.equal(result.error.error.code, 'UnknownEventType')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
