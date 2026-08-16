// FROZEN — 2026-08-14. Historical filename retained; Git snapshot merge was shock-cut.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { eventId, idValue, listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const Merge = await import('../../../dist/Persistence/EventStore/EventKWayMerge.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const make = (id, parents = [], stream = 'merge/main', payload = {}) => new Domain.EventEnvelope(
  eventId(id), streamId(stream), 'JobRequested', toList(parents.map(eventId)), payload, toList([]),
)
const ids = (result) => listItems(resultOf(result).value).map((e) => idValue.event(e.EventId))

const A = 'a'.repeat(40)
const B = 'b'.repeat(40)
const C = 'c'.repeat(40)

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops distinct events', () => {
  const result = Merge.merge(toList([['writer-a', toList([make(A)])], ['writer-b', toList([make(B)])]]))
  assert.deepEqual(ids(result).sort(), [A, B].sort())
})

test('WHAT[DURABLE-CONVERGENCE-002] writer enumeration is commutative', () => {
  const a = ['writer-a', toList([make(A), make(C, [A])])]
  const b = ['writer-b', toList([make(B)])]
  assert.deepEqual(ids(Merge.merge(toList([a, b]))), ids(Merge.merge(toList([b, a]))))
})

test('WHAT[DURABLE-CONVERGENCE-002] duplicate stream input is idempotent by EventId', () => {
  const event = make(A, [], 'merge/main', { x: 1 })
  const result = Merge.merge(toList([['a', toList([event])], ['copy', toList([event])]]))
  assert.deepEqual(ids(result), [A])
})

test('WHAT[DURABLE-CONVERGENCE-003] identity collision is fail closed not LWW', () => {
  const left = make(A, [], 'merge/main', { x: 1 })
  const right = make(A, [], 'merge/main', { x: 2 })
  const result = resultOf(Merge.merge(toList([['left', toList([left])], ['right', toList([right])]])))
  assert.equal(result.ok, false)
})
