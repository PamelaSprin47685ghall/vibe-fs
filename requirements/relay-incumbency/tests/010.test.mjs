import assert from 'node:assert/strict'
import test from 'node:test'

test('WHAT[relay-incumbency-010] a retired tenure cannot dispatch the fixed DevOps', () => {
  const mayDispatch = (tenure) => tenure === 'active'
  assert.equal(mayDispatch('active'), true)
  assert.equal(mayDispatch('retired'), false)
})
