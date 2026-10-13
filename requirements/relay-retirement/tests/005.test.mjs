import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce, deliverTwice } from '../../work/tests/support/contract.mjs'

test('WHAT[relay-retirement-005] S4 confirm does not finish, and S1-S3 is one call', () => {
  const first = deliverTwice({ sequence: ['S4'], reviewed: true, confirmed: false, reviewOutcome: 'Stayed' }, { toolCallId: 'c1', providerRunId: 'r1', head: 2 })
  assert.equal(first.finished, false)
  assert.equal(first.written[0].type, 'ConfirmationCommitted')
  const oneshot = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: [], deliveryAuthorized: false, head: 2, projection: 'S1', incumbencyId: 'i' })
  assert.equal(oneshot.finished, true)
  assert.equal(oneshot.written.some((event) => event.type === 'ConfirmationCommitted'), false)
})
