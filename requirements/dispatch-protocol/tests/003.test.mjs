import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity', () => {
  const receipt = dispatch.createAdmissionReceipt('accepted-123')
  assert.equal(receipt.isPhysical, false)
  assert.equal(receipt.id, 'accepted-123')
  assert.throws(() => dispatch.asPhysicalUserMessageId(receipt), /receipt is not a physical message id/)
})
