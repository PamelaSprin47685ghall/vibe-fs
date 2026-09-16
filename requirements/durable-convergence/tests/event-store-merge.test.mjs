// FROZEN — 2026-08-14. Convergence laws over the public EventStore merge owner.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const make = (id, parents = [], stream = 'merge/main', payload = {}) => ({
  id,
  stream,
  type: 'JobRequested',
  parents,
  payload,
  payloadRefs: [],
})
const ids = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.events.map((event) => event.id)
}

const A = 'a'.repeat(40)
const B = 'b'.repeat(40)
const C = 'c'.repeat(40)

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops distinct events', () => {
  const result = merge.merge([
    ['writer-a', [make(A)]],
    ['writer-b', [make(B)]],
  ])
  assert.deepEqual(ids(result).sort(), [A, B].sort())
})

test('WHAT[DURABLE-CONVERGENCE-002] writer enumeration is commutative', () => {
  const a = ['writer-a', [make(A), make(C, [A])]]
  const b = ['writer-b', [make(B)]]
  assert.deepEqual(ids(merge.merge([a, b])), ids(merge.merge([b, a])))
})

test('WHAT[DURABLE-CONVERGENCE-002] duplicate stream input is idempotent by EventId', () => {
  const event = make(A, [], 'merge/main', { x: 1 })
  const result = merge.merge([
    ['writer-a', [event]],
    ['writer-copy', [event]],
  ])
  assert.deepEqual(ids(result), [A])
})

test('WHAT[DURABLE-CONVERGENCE-003] identity collision is fail closed not LWW', () => {
  const left = make(A, [], 'merge/main', { x: 1 })
  const right = make(A, [], 'merge/main', { x: 2 })
  const result = merge.merge([
    ['writer-left', [left]],
    ['writer-right', [right]],
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'IdentityCollision')
})
