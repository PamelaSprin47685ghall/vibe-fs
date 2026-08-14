// FROZEN — 2026-08-14. Convergence laws over EventKWayMerge + Integrator structural Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { eventId, idValue, listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const Merge = await import('../../../dist/Persistence/EventStore/EventKWayMerge.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const make = (id, parents = [], stream = 'replica/law', type = 'JobRequested', payload = {}) => new Domain.EventEnvelope(
  eventId(id), streamId(stream), type, toList(parents.map(eventId)), payload, toList([]),
)
const ids = (r) => listItems(resultOf(r).value).map((e) => idValue.event(e.EventId))
const A = 'a'.repeat(40), B = 'b'.repeat(40), C = 'c'.repeat(40), R = 'd'.repeat(40)

test('set_union_never_drops_concurrent_events', () => {
  const merged = ids(Merge.merge(toList([['a', toList([make(A)])], ['b', toList([make(B)])]])))
  assert.deepEqual(new Set(merged), new Set([A, B]))
})

test('merge_is_commutative_associative_idempotent_at_writer_stream_level', () => {
  const sa = ['a', toList([make(A)])]
  const sb = ['b', toList([make(B)])]
  const sc = ['c', toList([make(C)])]
  const abc = ids(Merge.merge(toList([sa, sb, sc])))
  const cba = ids(Merge.merge(toList([sc, sb, sa])))
  assert.deepEqual(abc, cba)
  assert.deepEqual(ids(Merge.merge(toList([sa, ['copy', toList([make(A)])]]))), [A])
})

test('convergence_is_a_function_of_event_truth_not_arrival_wall_clock', () => {
  const streams1 = toList([['a', toList([make(A), make(C, [A])])], ['b', toList([make(B)])]])
  const streams2 = toList([['b', toList([make(B)])], ['a', toList([make(A), make(C, [A])])]])
  assert.deepEqual(ids(Merge.merge(streams1)), ids(Merge.merge(streams2)))
})

test('concurrent_heads_are_preserved_as_structural_DomainConflict_frontier', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('replica/conflict')
    const a = make(A, [], 'replica/conflict')
    const b = make(B, [], 'replica/conflict')
    assert.equal(resultOf(await local.store.Append(toList([a]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([b]))).ok, true)
    const heads = listItems(local.store.TryHeads(sid)).map(idValue.event).sort()
    assert.deepEqual(heads, [A, B].sort())
    assert.equal(local.store.TryHead(sid), undefined, 'conflict must not masquerade as one linear head')
  } finally {
    local.close()
  }
})

test('resolution_with_all_competing_heads_collapses_structural_frontier', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('replica/resolution')
    const a = make(A, [], 'replica/resolution')
    const b = make(B, [], 'replica/resolution')
    const resolution = make(R, [A, B], 'replica/resolution', 'JobConflictResolved', { winner: A })
    assert.equal(resultOf(await local.store.Append(toList([a]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([b]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([resolution]))).ok, true)
    assert.deepEqual(listItems(local.store.TryHeads(sid)).map(idValue.event), [R])
    assert.equal(idValue.event(local.store.TryHead(sid)), R)
  } finally {
    local.close()
  }
})
