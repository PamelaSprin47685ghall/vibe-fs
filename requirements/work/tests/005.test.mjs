import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce } from './support/contract.mjs'

test('WHAT[work-005] S1-S3 deliver completes or relays in a single call without ritual', () => {
  const relayed = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: [], deliveryAuthorized: false, head: 7, projection: 'S1', incumbencyId: 'i' })
  assert.equal(relayed.finished, true)
  assert.equal(relayed.outcome, 'Relayed')
  assert.equal(relayed.written.map((event) => event.type).join(','), 'IncumbencyRetired,IncumbencyOpened')
  assert.equal(relayed.written[0].outcome, 'Relayed')
  assert.equal(relayed.written[1].openingCursor, 7)
  const delivered = deliverOnce({
    sequence: ['S1', 'S2', 'S3'],
    blockers: [],
    deliveryAuthorized: true,
    head: 8,
    incumbencyId: 'i',
    execSessionId: 'e',
    digest: 'd',
    path: 'p',
  })
  assert.equal(delivered.outcome, 'Delivered')
  assert.equal(delivered.written.length, 1)
})
