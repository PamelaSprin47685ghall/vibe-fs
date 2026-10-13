import assert from 'node:assert/strict'
import test from 'node:test'
import { review } from './support/contract.mjs'

test('WHAT[work-003] Promoted authorizes only a one-shot final stage', () => {
  const stayed = review({ active: true, reviewed: false, sequence: ['S1', 'S2', 'S3'], projection: 'S1', incumbencyId: 'i' }, 'Stayed')
  assert.equal(stayed.deliveryAuthorized, false)
  assert.equal(stayed.projection, 'S1')
  const promoted = review({ active: true, reviewed: false, sequence: ['S1', 'S2', 'S3'], projection: 'S3', incumbencyId: 'i' }, 'Promoted')
  assert.equal(promoted.deliveryAuthorized, true)
  const s4 = review({ active: true, reviewed: false, sequence: ['S4'], projection: 'S4', incumbencyId: 'i' }, 'Promoted')
  assert.equal(s4.deliveryAuthorized, false)
  assert.equal(s4.written[0].outcome, 'Promoted')
  assert.equal('snapshotId' in s4.written[0], false)
})
