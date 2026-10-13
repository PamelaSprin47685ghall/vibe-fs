import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce, relayHandover } from './support/contract.mjs'

test('WHAT[work-044] Relayed keeps the fixed DevOps and its received work for the successor', () => {
  const relayed = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: [], deliveryAuthorized: false, head: 7, projection: 'S1', incumbencyId: 'i' })
  const handover = relayHandover(relayed, { devopsId: 'd', received: ['job-1'] })
  assert.equal(handover.ok, true)
  assert.equal(handover.orphaned, false)
  assert.deepEqual(handover.kept, ['job-1'])
  assert.equal(handover.written.some((event) => event.type === 'DevOpsBound'), false)
})
