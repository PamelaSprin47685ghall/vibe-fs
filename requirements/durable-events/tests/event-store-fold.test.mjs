// Shock-cut structural fold contract: history ordering is EventKWayMerge and
// Current ownership is CanonicalIntegrator. There is no standalone EventStoreFold.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, idValue, listItems, payloadOf, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Merge = await import('../../../dist/Infrastructure/Persist/EventKWayMerge.js')

const streamId = (value) => Domain.EventStreamIdModule_create(value)
const make = ({
  id,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
} = {}) => new Domain.EventEnvelope(
  eventId(id),
  streamId(stream),
  eventType,
  toList(parents.map(eventId)),
  payload,
  toList([]),
)

const ids = (result) => listItems(resultOf(result).value).map((event) => idValue.event(event.EventId))
const A = 'a'.repeat(40)
const B = 'b'.repeat(40)
const C = 'c'.repeat(40)
const D = 'd'.repeat(40)

test('DURABLE_EVENTS_014_k_way_merge_rejects_missing_parent_fail_closed', () => {
  const child = make({ id: B, parents: [A] })
  const result = resultOf(Merge.merge(toList([['writer', toList([child])]])))
  assert.equal(result.ok, false)
  assert.equal(caseOf(result.error), 'MissingParent')
  assert.equal(idValue.event(payloadOf(result.error)), A)
})

test('DURABLE_EVENTS_014_k_way_merge_rejects_backward_or_cyclic_writer_frontier', () => {
  const a = make({ id: A, parents: [B] })
  const b = make({ id: B, parents: [A] })
  const result = resultOf(Merge.merge(toList([['writer', toList([a, b])]])))
  assert.equal(result.ok, false)
  assert.equal(caseOf(result.error), 'NonCanonical')
})

test('DURABLE_EVENTS_014_k_way_merge_is_deterministic_with_EventId_tiebreak', () => {
  const root = make({ id: A })
  const high = make({ id: C, parents: [A], eventType: 'JobAccepted' })
  const low = make({ id: B, parents: [A], eventType: 'JobRejected' })

  const first = ids(Merge.merge(toList([
    ['writer-a', toList([root, high])],
    ['writer-b', toList([low])],
  ])))
  const second = ids(Merge.merge(toList([
    ['writer-b', toList([low])],
    ['writer-a', toList([root, high])],
  ])))

  assert.deepEqual(first, [A, B, C])
  assert.deepEqual(second, first)
})

test('DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('job/conflict')
    assert.equal(resultOf(await local.store.Append(toList([
      make({ id: A, stream: 'job/conflict' }),
    ]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([
      make({ id: B, stream: 'job/conflict' }),
    ]))).ok, true)

    assert.deepEqual(listItems(local.store.TryHeads(sid)).map(idValue.event).sort(), [A, B])
    assert.equal(local.store.TryHead(sid), undefined, 'a fork must not masquerade as a unique head')
  } finally {
    local.close()
  }
})

test('DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('job/resolution')
    assert.equal(resultOf(await local.store.Append(toList([
      make({ id: A, stream: 'job/resolution' }),
    ]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([
      make({ id: B, stream: 'job/resolution' }),
    ]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([
      make({ id: D, stream: 'job/resolution', eventType: 'JobConflictResolved', parents: [A, B] }),
    ]))).ok, true)

    assert.deepEqual(listItems(local.store.TryHeads(sid)).map(idValue.event), [D])
    assert.equal(idValue.event(local.store.TryHead(sid)), D)
  } finally {
    local.close()
  }
})

test('DURABLE_EVENTS_007_unknown_authoritative_event_type_is_rejected_before_durability', async () => {
  const local = createLocalEventStore()
  try {
    const result = resultOf(await local.store.Append(toList([
      make({ id: A, eventType: 'TotallyUnknownEventType' }),
    ])))
    assert.equal(result.ok, false)
    assert.equal(caseOf(result.error), 'StorageInvalid')
    assert.equal(caseOf(payloadOf(result.error)), 'UnknownEventType')
  } finally {
    local.close()
  }
})
