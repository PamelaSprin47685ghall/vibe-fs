import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'
import * as joinGuardSurface from '../../../dist/Interaction/Dispatch/JoinGuardSurface.js'

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'tool',
    payloadDigest: 'sha256-payload',
    participant: 'agent-1',
    role: 'coder',
  })
  dispatch.submitClaim(store, key, { receiptId: 'rec-1' })
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Submitted')
})

test('WHAT[DISPATCH-PROTOCOL-007] PROMPT_007_detached_refused_abandons_send_failed_without_resend', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'detached',
    payloadDigest: 'sha256-payload',
    participant: 'agent-1',
    role: 'coder',
  })
  dispatch.abandonClaim(store, key, 'SendFailed')
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Abandoned')
  assert.equal(snap.abandonReason, 'SendFailed')
})

test('WHAT[DISPATCH-PROTOCOL-007] PROMPT_007_detached_outcome_unknown_keeps_claim_pending_never_resends', () => {
  const store = dispatch.createMemoryStore()
  const key = dispatch.registerClaim(store, {
    sessionId: 'ses-1',
    logicalRunId: 'run-1',
    authorityRootId: 'auth-1',
    origin: 'detached',
    payloadDigest: 'sha256-payload',
    participant: 'agent-1',
    role: 'coder',
  })
  const snap = dispatch.claimSnapshot(store, key)
  assert.equal(snap.status, 'Claimed')
})

test('WHAT[DISPATCH-PROTOCOL-007] join_guard_rejects_unsettled_claim_resend', () => {
  const guard = dispatch.createJoinGuard()
  assert.equal(dispatch.joinGuardCanDispatch(guard, 'pending-key'), false)
})
