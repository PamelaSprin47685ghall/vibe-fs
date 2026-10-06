import assert from 'node:assert/strict'
import test from 'node:test'
import {inspectRegistry, declaration, manifest, permutations} from './registry-support.mjs'

function accepted(values) {
  const result = inspectRegistry(values.map(value => declaration(value)))
  assert.equal(result.ok, true)
  assert.deepEqual(result.value.lock, result.value.ordered)
  return result.value.ordered
}

test('WHAT[sphinx-v2-009] the original registry binds a shared dependency once before its consumer for either declaration order', () => {
  const parent = manifest('A')
  const consumer = manifest('B', ['A'])
  for (const values of [[parent, consumer], [consumer, parent]]) {
    assert.deepEqual(accepted(values), [parent, consumer])
  }
})

test('WHAT[sphinx-v2-009] a diamond produces each plugin once and keeps every dependency before its consumer across declaration permutations', () => {
  const values = [manifest('A'), manifest('B', ['A']), manifest('C', ['A']), manifest('D', ['B', 'C'])]
  for (const order of permutations(values)) {
    const bound = accepted(order)
    assert.deepEqual(bound, values)
    assert.equal(new Set(bound.map(value => value.id)).size, bound.length)
    for (const plugin of bound) {
      for (const dependency of plugin.dependencies) {
        assert.ok(bound.findIndex(value => value.id === dependency) < bound.findIndex(value => value.id === plugin.id))
      }
    }
  }
})

test('WHAT[sphinx-v2-009] independent registry roots stay distinct and deterministic without duplicated dependencies', () => {
  const values = [manifest('A'), manifest('B', ['A']), manifest('C')]
  for (const order of permutations(values)) assert.deepEqual(accepted(order), values)
  assert.deepEqual(accepted([]), [])
})

for (const [label, values, code] of [
  ['missing dependency', [manifest('A', ['absent'])], 'plugin-dependency-missing'],
  ['dependency cycle', [manifest('A', ['B']), manifest('B', ['A'])], 'plugin-cycle'],
  ['multiple releases', [manifest('A'), {...manifest('A'), release: 'other@2'}], 'plugin-conflict'],
]) {
  test(`WHAT[sphinx-v2-009] original ${label} admission still fails closed`, () => {
    const rejected = inspectRegistry(values.map(value => declaration(value)))
    assert.equal(rejected.ok, false)
    assert.equal(rejected.error.code, code)
    assert.notEqual(rejected.error.message.trim(), '')
  })
}
