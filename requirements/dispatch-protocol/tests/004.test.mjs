import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-004] ingress_accepts_the_exact_nonblank_Host_identity', () => {
  const result = dispatch.decodeIngressMessageId({ messageID: 'msg-valid-1' })
  assert.equal(result.ok, true)
  assert.equal(result.id, 'msg-valid-1')
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_missing_or_blank_Host_identity', () => {
  assert.equal(dispatch.decodeIngressMessageId({}).ok, false)
  assert.equal(dispatch.decodeIngressMessageId({ messageID: '' }).ok, false)
  assert.equal(dispatch.decodeIngressMessageId({ messageID: '   ' }).ok, false)
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_conflicting_Host_identity_carriers', () => {
  const result = dispatch.decodeIngressMessageId({
    input: { messageID: 'msg-1' },
    output: { message: { id: 'msg-2' } },
  })
  assert.equal(result.ok, false)
})

test('WHAT[DISPATCH-PROTOCOL-004] ingress_ignores_non_contract_identity_decoys', () => {
  const result = dispatch.decodeIngressMessageId({
    messageID: 'msg-1',
    decoyId: 'msg-fake',
  })
  assert.equal(result.ok, true)
  assert.equal(result.id, 'msg-1')
})

test('WHAT[DISPATCH-PROTOCOL-004] DP_004_physical_acceptance_is_proven_only_by_physical_message', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'test',
    payloadDigest: 'sha256-payload',
    participant: 'agent-1',
    role: 'coder',
  })
  dispatch.resolvePhysicalAccepted(store, key, { physicalMessageId: 'phys-msg-1' })
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'PhysicalAccepted')
  assert.equal(snap.physicalMessageId, 'phys-msg-1')
})
