import assert from 'node:assert/strict'
import test from 'node:test'
import { call, decode } from './support/cycle.mjs'

test('WHAT[behavior-diagnosis-010] actual cycle validation rejects blank provider identity', () => {
  for (const id of ['', '   ']) {
    const result = decode([call()], id)
    assert.equal(result.decision.ok, false)
    assert.match(result.decision.error, /no provable provider run/)
  }
})

test('WHAT[behavior-diagnosis-010] actual decoder preserves provider and tool identities and refuses absent tool identity', () => {
  const result = decode([call({ callID: 'exact-tool' })], 'exact-provider')
  assert.equal(result.messageId, 'exact-provider')
  assert.deepEqual(result.decision.value.toolCallIds, ['exact-tool'])
  const missing = decode([call({ callID: undefined })])
  assert.equal(missing.decodedCalls, 0)
  assert.equal(missing.decision.ok, false)
})

test.todo('WHAT[behavior-diagnosis-010] GAP-113 settle conflict between fatal missing identity and attempt-only termination before claiming full compliance')
