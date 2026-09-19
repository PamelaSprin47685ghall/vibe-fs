import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-007] post-fission completion has exactly one affinity target: the initiating lane', () => {
  assert.deepEqual(fission.completionTargets(4, { kind: 'lane', index: 2 }), [2])
})
