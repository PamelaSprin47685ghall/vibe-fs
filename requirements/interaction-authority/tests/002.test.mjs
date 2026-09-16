import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

test('WHAT[INTERACTION-AUTHORITY-002] IA_002_transport_receipt_shape_is_not_authority_evidence', () => {
  assert.equal(authority.transportReceiptShape('accepted-1a2b'), true)
  assert.equal(authority.transportReceiptShape('msg_real'), false)
})
