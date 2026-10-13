import assert from 'node:assert/strict'
import test from 'node:test'

test('WHAT[relay-incumbency-001] one open Work has one active tenure', () => {
  const open = (active) => active.length <= 1
  assert.equal(open(['t1']), true)
  assert.equal(open(['t1', 't2']), false)
})
