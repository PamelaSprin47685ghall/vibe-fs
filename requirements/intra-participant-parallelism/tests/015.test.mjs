import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-015] pure ring topology and work bundle order are independent of record arrival', () => {
  assert.deepEqual(fission.ringMergeOrder(4), [0, 1, 2, 3])
  assert.equal(fission.ringFinalLane(4), 3)
  assert.deepEqual(fission.ringMergeOrder(2), [0, 1])
  assert.equal(fission.ringFinalLane(2), 1)
  assert.deepEqual(fission.ringMergeOrder(1), [])
  assert.equal(fission.ringFinalLane(1), null)
  for (const arrival of [[0, 1, 2], [2, 1, 0], [1, 2, 0], [2, 0, 1]]) {
    const bundle = arrival.reduce(
      (state, lane) => mustOk(fission.workBundleAdd(lane, `record-${lane}`, state)).bundle,
      fission.workBundleEmpty,
    )
    assert.deepEqual(fission.workBundleKeys(bundle), [0, 1, 2])
    assert.equal(fission.ringFinalLane(3), 2)
  }
})

test.todo('WHAT[intra-participant-parallelism-015] GAP-158: actual Host takeover always selects N-1 under reordered lane completion')
