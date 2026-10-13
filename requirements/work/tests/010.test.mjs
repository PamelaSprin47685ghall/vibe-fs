import assert from 'node:assert/strict'
import test from 'node:test'
import { admitOldBytes } from './support/contract.mjs'

test('WHAT[work-010] old log bytes are not input and are not translated into a new event', () => {
  const old = '{"event_type":"RetirementCommitted","payload":{"certificateId":"c"}}'
  const admitted = admitOldBytes(old)
  assert.equal(admitted.admitted, false)
  assert.equal(admitted.reason, 'old-bytes-are-not-input')
})
