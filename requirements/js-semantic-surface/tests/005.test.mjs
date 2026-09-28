import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'

class RuntimeValue {
  constructor() { Object.assign(this, { tag: 0, fields: ['value'] }) }
}

test('WHAT[js-semantic-surface-005] the validator accepts the declared JS-native values without reserving ordinary field names', () => {
  const values = [
    undefined, null, 's', 42, true, 10n, [], { a: [1, { b: 'c' }] }, () => 1,
    Object.assign(Object.create(null), { value: 1 }),
    { tag: 'article', fields: ['title'], head: 'heading', tail: 'tail', cases: () => [] },
  ]
  for (const value of values) {
    assert.equal(isJsData(value), true)
    assert.equal(assertJsData(value), value, 'validation must preserve the boundary value')
  }
})

test('WHAT[js-semantic-surface-005] Promise is a permitted carrier and its fulfilled value is validated separately', async () => {
  const value = { ok: true, value: [1, 2] }
  const promise = Promise.resolve(value)
  assert.equal(isJsData(promise), true)
  assert.equal(assertJsData(promise), promise)
  assert.deepEqual(assertJsData(await promise), value)
  const leaked = Promise.resolve(new RuntimeValue())
  const awaitedValue = await leaked
  assert.throws(() => assertJsData(awaitedValue), /JS-native/)
})

test('WHAT[js-semantic-surface-005] runtime instances and nested non-native values are rejected', () => {
  for (const value of [
    new RuntimeValue(), new Date(), new Map(), new Set(), Symbol('hidden'),
    { nested: [new RuntimeValue()] },
  ]) {
    assert.equal(isJsData(value), false)
    assert.throws(() => assertJsData(value), /JS-native/)
  }
  assert.equal(isJsData({ at: '2026-09-26T00:00:00Z', epochMs: 1790380800000 }), true)
})

test('WHAT[js-semantic-surface-005] shared and cyclic native data remain native', () => {
  const shared = { value: 1 }
  const graph = { left: shared, right: shared }
  graph.self = graph
  assert.equal(assertJsData(graph), graph)
  graph.invalid = new RuntimeValue()
  assert.equal(isJsData(graph), false)
})

test('WHAT[js-semantic-surface-005] opaque validation preserves identity without reading private state', () => {
  const handle = Object.create(null)
  Object.defineProperty(handle, 'privateState', { get() { assert.fail('opaque internals must not be read') } })
  assert.equal(assertOpaque(handle), handle)
  const callable = () => 1
  assert.equal(assertOpaque(callable), callable)
  for (const value of [undefined, null, 's', 1, false]) assert.throws(() => assertOpaque(value), /opaque/)
})
