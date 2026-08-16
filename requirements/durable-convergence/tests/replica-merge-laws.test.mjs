// FROZEN — 2026-08-14. Convergence laws over EventKWayMerge + Integrator structural Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { eventId, FsList, idValue, listItems } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const Merge = await import('../../../dist/Persistence/EventStore/EventKWayMerge.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const make = (id, parents = [], stream = 'replica/law', type = 'JobRequested', payload = {}) => new Domain.EventEnvelope(
  eventId(id), streamId(stream), type, FsList.ofArray(parents.map(eventId)), payload, FsList.ofArray([]),
)
const ids = (r) => listItems(r.toJSON()[1]).map((e) => idValue.event(e.EventId))
const A = 'a'.repeat(40), B = 'b'.repeat(40), C = 'c'.repeat(40), R = 'd'.repeat(40)

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops concurrent events', () => {
  const merged = ids(Merge.merge(FsList.ofArray([['a', FsList.ofArray([make(A)])], ['b', FsList.ofArray([make(B)])]])))
  assert.deepEqual(new Set(merged), new Set([A, B]))
})

test('WHAT[DURABLE-CONVERGENCE-002] merge is commutative associative idempotent at writer stream level', () => {
  const sa = ['a', FsList.ofArray([make(A)])]
  const sb = ['b', FsList.ofArray([make(B)])]
  const sc = ['c', FsList.ofArray([make(C)])]
  const abc = ids(Merge.merge(FsList.ofArray([sa, sb, sc])))
  const cba = ids(Merge.merge(FsList.ofArray([sc, sb, sa])))
  assert.deepEqual(abc, cba)
  assert.deepEqual(ids(Merge.merge(FsList.ofArray([sa, ['copy', FsList.ofArray([make(A)])]]))), [A])
})

test('WHAT[DURABLE-CONVERGENCE-006] convergence is a function of event truth not arrival wall clock', () => {
  const streams1 = FsList.ofArray([['a', FsList.ofArray([make(A), make(C, [A])])], ['b', FsList.ofArray([make(B)])]])
  const streams2 = FsList.ofArray([['b', FsList.ofArray([make(B)])], ['a', FsList.ofArray([make(A), make(C, [A])])]])
  assert.deepEqual(ids(Merge.merge(streams1)), ids(Merge.merge(streams2)))
})

test('WHAT[DURABLE-CONVERGENCE-004] concurrent heads are preserved as structural DomainConflict frontier', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('replica/conflict')
    const a = make(A, [], 'replica/conflict')
    const b = make(B, [], 'replica/conflict')
    assert.equal((await local.store.Append(FsList.ofArray([a]))).toJSON()[0], 'Ok')
    assert.equal((await local.store.Append(FsList.ofArray([b]))).toJSON()[0], 'Ok')
    const heads = listItems(local.store.TryHeads(sid)).map(idValue.event).sort()
    assert.deepEqual(heads, [A, B].sort())
    assert.equal(local.store.TryHead(sid), undefined, 'conflict must not masquerade as one linear head')
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-CONVERGENCE-005] resolution with all competing heads collapses structural frontier', async () => {
  const local = createLocalEventStore()
  try {
    const sid = streamId('replica/resolution')
    const a = make(A, [], 'replica/resolution')
    const b = make(B, [], 'replica/resolution')
    const resolution = make(R, [A, B], 'replica/resolution', 'JobConflictResolved', { winner: A })
    assert.equal((await local.store.Append(FsList.ofArray([a]))).toJSON()[0], 'Ok')
    assert.equal((await local.store.Append(FsList.ofArray([b]))).toJSON()[0], 'Ok')
    assert.equal((await local.store.Append(FsList.ofArray([resolution]))).toJSON()[0], 'Ok')
    assert.deepEqual(listItems(local.store.TryHeads(sid)).map(idValue.event), [R])
    assert.equal(idValue.event(local.store.TryHead(sid)), R)
  } finally {
    local.close()
  }
})
