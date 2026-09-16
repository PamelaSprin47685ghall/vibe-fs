import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_abandon_keeps_the_claim_sequence_consumed', () => {
  const store = dispatch.createMemoryStore()
  const claim = {
    sessionId: 's1',
    logicalRunId: 'r1',
    authorityRootId: 'a1',
    origin: 'test',
    payloadDigest: 'd1',
    participant: 'p1',
    role: 'coder',
  }
  const k1 = dispatch.registerClaim(store, claim)
  assert.equal(dispatch.claimSnapshot(store, k1).sequence, 0)
  dispatch.abandonClaim(store, k1, 'SendFailed')

  const k2 = dispatch.registerClaim(store, claim)
  assert.equal(dispatch.claimSnapshot(store, k2).sequence, 1)
  assert.notEqual(k1, k2)
})

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution', () => {
  const store = dispatch.createMemoryStore()
  const claim = {
    sessionId: 's1',
    logicalRunId: 'r1',
    authorityRootId: 'a1',
    origin: 'test',
    payloadDigest: 'd1',
    participant: 'p1',
    role: 'coder',
  }
  const k1 = dispatch.registerClaim(store, claim)
  const k2 = dispatch.registerClaim(store, claim)
  assert.equal(dispatch.claimSnapshot(store, k1).sequence, 0)
  assert.equal(dispatch.claimSnapshot(store, k2).sequence, 1)
})
