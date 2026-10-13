import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce } from './support/contract.mjs'

test('WHAT[work-009] a live owned resource blocks deliver without retiring', () => {
  const blocked = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: ['pty'], deliveryAuthorized: false, head: 1, projection: 'S1', incumbencyId: 'i' })
  assert.equal(blocked.written[0].type, 'RetirementBlocked')
  assert.equal(blocked.written.some((event) => event.type === 'IncumbencyRetired'), false)
  assert.equal(blocked.finished, false)
})
