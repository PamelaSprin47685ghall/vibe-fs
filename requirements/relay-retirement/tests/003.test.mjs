import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce } from '../../work/tests/support/contract.mjs'

test('WHAT[relay-retirement-003] a live owned resource blocks deliver', () => {
  const blocked = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: ['pty'], deliveryAuthorized: false, head: 1, projection: 'S1', incumbencyId: 'i' })
  assert.equal(blocked.written[0].type, 'RetirementBlocked')
  assert.equal(blocked.written.some((event) => event.type === 'IncumbencyRetired'), false)
})
