import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-008] keyed work bundle is idempotent and rejects conflicting records for one lane', () => {
  const empty = fission.workBundleEmpty
  assertJsData(empty, 'workBundleEmpty')
  assert.deepEqual(empty, { entries: [] })
  const a = mustOk(fission.workBundleAdd(2, 'ref-c', empty)).bundle
  const b = mustOk(fission.workBundleAdd(0, 'ref-a', a)).bundle
  const same = mustOk(fission.workBundleAdd(0, 'ref-a', b)).bundle
  assert.deepEqual(fission.workBundleKeys(same), [0, 2])

  const conflict = fission.workBundleAdd(0, 'ref-other', same)
  assert.equal(conflict.ok, false)
  assert.equal(conflict.reason, 'ConflictingLaneRecord')

  const left = mustOk(fission.workBundleAdd(1, 'ref-b', b)).bundle
  const right = mustOk(fission.workBundleAdd(1, 'ref-b', mustOk(fission.workBundleAdd(0, 'ref-a', empty)).bundle)).bundle
  const merged1 = mustOk(fission.workBundleMerge(left, right)).bundle
  const merged2 = mustOk(fission.workBundleMerge(right, left)).bundle
  assert.deepEqual(fission.workBundleEntries(merged1), fission.workBundleEntries(merged2))
})
