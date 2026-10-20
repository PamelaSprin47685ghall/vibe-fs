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
  // 未交付 lane 保持 group 并发 nudge，不把整组失败当正确。
  // Conflicting lane records fail the bundle mutation without destroying the aggregate.
  const conflict = fission.workBundleAdd(0, 'conflicting-ref', mustOk(fission.workBundleAdd(0, 'record-0', fission.workBundleEmpty)).bundle)
  assert.equal(conflict.ok, false)
  assert.equal(conflict.reason, 'ConflictingLaneRecord')
  assert.equal(fission.ringFinalLane(0), null)
  assert.deepEqual(fission.ringMergeOrder(0), [])
})

test.todo('WHAT[intra-participant-parallelism-015] GAP-158: actual Host takeover always selects N-1 under reordered lane completion')
