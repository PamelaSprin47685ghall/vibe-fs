import assert from 'node:assert/strict'
import test from 'node:test'
import { openWork } from './support/contract.mjs'

test('WHAT[work-002] opening writes sequence and refuses snapshotId', () => {
  const plan = openWork({ workId: 'w', root: 'root', sequence: ['S1', 'S2', 'S3'], incumbencyId: 'i', observedHead: 4 })
  assert.equal(plan.ok, true)
  assert.equal(plan.confirmationCount, 1)
  assert.equal(plan.written[1].openingCursor, 4)
  assert.equal('snapshotId' in plan.written[0], false)
  const manager = openWork({ workId: 'm', root: 'root', sequence: ['S4'], incumbencyId: 'j', observedHead: 9 })
  assert.equal(manager.confirmationCount, 2)
  assert.equal(openWork({ workId: 'x', root: 'root', sequence: ['S1', 'S2', 'S3', 'S4'], incumbencyId: 'k', observedHead: 1 }).ok, false)
  assert.equal(openWork({ workId: 'y', root: 'root', sequence: ['S4'], incumbencyId: 'k', observedHead: 1, snapshotId: 's' }).ok, false)
})
