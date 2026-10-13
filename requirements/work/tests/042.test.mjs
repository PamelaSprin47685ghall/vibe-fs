import assert from 'node:assert/strict'
import test from 'node:test'
import { bindDevOps } from './support/contract.mjs'

test('WHAT[work-042] a Work binds one DevOps and does not fork a second', () => {
  const first = bindDevOps({ workId: 'w', devopsId: 'd', bound: false })
  assert.equal(first.ok, true)
  assert.deepEqual(first.written.map((event) => event.type), ['DevOpsBound'])
  const again = bindDevOps({ workId: 'w', devopsId: 'd-2', bound: true })
  assert.equal(again.ok, true)
  assert.deepEqual(again.written, [])
})
