// Two ordinary passing leaf tests with names that cannot match a file wrapper.
import test from 'node:test'
import assert from 'node:assert/strict'

test('leaf one', () => {
  assert.equal(1, 1)
})

test('leaf two', () => {
  assert.equal(2, 2)
})
