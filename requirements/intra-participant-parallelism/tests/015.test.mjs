import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-015] ring fold order and final takeover lane are canonical, never arrival ordered', () => {
  assert.deepEqual(fission.ringMergeOrder(4), [0, 1, 2, 3])
  assert.equal(fission.ringFinalLane(4), 3)
  assert.deepEqual(fission.ringMergeOrder(2), [0, 1])
  assert.equal(fission.ringFinalLane(2), 1)
  assert.deepEqual(fission.ringMergeOrder(1), [])
  assert.equal(fission.ringFinalLane(1), null)
})
