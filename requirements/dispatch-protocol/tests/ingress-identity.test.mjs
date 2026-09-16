import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const physicalIdentity = (input, output) => dispatch.decodePhysicalUserMessageId(input, output)

test('WHAT[DISPATCH-PROTOCOL-004] ingress_accepts_the_exact_nonblank_Host_identity', () => {
  assert.equal(physicalIdentity({ messageID: 'msg-input' }, {}), 'msg-input')
  assert.equal(physicalIdentity({}, { message: { id: 'msg-output' } }), 'msg-output')
  assert.equal(
    physicalIdentity({ messageID: 'msg-shared' }, { message: { id: 'msg-shared' } }),
    'msg-shared',
  )
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_missing_or_blank_Host_identity', () => {
  assert.equal(physicalIdentity({}, {}), null)
  assert.equal(physicalIdentity({ messageID: '   ' }, { message: { id: '   ' } }), null)
  assert.equal(physicalIdentity({ messageID: '   ' }, { message: { id: 'msg-valid' } }), 'msg-valid')
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_conflicting_Host_identity_carriers', () => {
  assert.equal(
    physicalIdentity({ messageID: 'msg-input' }, { message: { id: 'msg-output' } }),
    null,
  )
  assert.equal(
    physicalIdentity({ messageID: 'msg-exact' }, { message: { id: ' msg-exact ' } }),
    null,
  )
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_ignores_non_contract_identity_decoys', () => {
  assert.equal(physicalIdentity({}, { id: 'msg-output' }), null)
  assert.equal(physicalIdentity({}, { info: { id: 'msg-info' } }), null)
  assert.equal(physicalIdentity({ messageId: 'msg-wrong-case' }, {}), null)
})
