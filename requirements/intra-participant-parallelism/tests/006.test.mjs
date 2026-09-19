import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[intra-participant-parallelism-006] pre-fission completion broadcasts to every lane exactly once with idempotent delivery', () => {
  const emptyDelivery = fission.deliveryEmpty(3)
  assertJsData(emptyDelivery, 'deliveryEmpty')
  assert.deepEqual(emptyDelivery, { laneCount: 3, deliveries: [] })
  assert.deepEqual(fission.completionTargets(4, { kind: 'pre-fission' }), [0, 1, 2, 3])

  let delivery = emptyDelivery
  delivery = mustOk(fission.deliveryMark('child-A', 0, delivery)).delivery
  delivery = mustOk(fission.deliveryMark('child-A', 0, delivery)).delivery // idempotent
  delivery = mustOk(fission.deliveryMark('child-A', 2, delivery)).delivery
  assert.deepEqual(fission.deliveryPendingTargets('child-A', delivery), [1])
})
