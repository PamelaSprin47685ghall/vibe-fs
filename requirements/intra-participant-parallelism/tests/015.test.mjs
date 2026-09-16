import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-015] ring fold order and final takeover lane are canonical, never arrival ordered', () => {
  assert.deepEqual(fission.ringMergeOrder(4), [0, 1, 2, 3])
  assert.equal(fission.ringFinalLane(4), 3)
  assert.deepEqual(fission.ringMergeOrder(2), [0, 1])
  assert.equal(fission.ringFinalLane(2), 1)
  assert.deepEqual(fission.ringMergeOrder(1), [])
  assert.equal(fission.ringFinalLane(1), null)
})
