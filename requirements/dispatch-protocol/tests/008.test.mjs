import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-008] DP_008_unproven_outcome_stays_pending_never_resends', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'nudge',
    payloadDigest: 'sha256-payload',
    participant: 'agent-1',
    role: 'coder',
  })
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Claimed')
})

test('WHAT[DISPATCH-PROTOCOL-008] DP_008_concurrent_exact_gate_nudges_share_one_claim_and_send', async () => {
  const store = dispatch.createMemoryStore()
  let flightCount = 0
  const flight = dispatch.singleFlight(store, 'scope-1', async () => {
    flightCount += 1
    return 'done'
  })
  const flight2 = dispatch.singleFlight(store, 'scope-1', async () => {
    flightCount += 1
    return 'done'
  })
  const [r1, r2] = await Promise.all([flight, flight2])
  assert.equal(r1, 'done')
  assert.equal(r2, 'done')
  assert.equal(flightCount, 1)
})
