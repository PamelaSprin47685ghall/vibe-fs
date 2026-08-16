// REVIEW-010/013: PendingSeal crosses the Review assurance owner boundary as
// plain strings and arrays; the SharedState map remains production-owned.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const sealValue = review.pendingSeal({
  SessionId: 'ses_seal',
  ManagerSessionId: 'ses_manager',
  BarrierId: 'bar_seal',
  GitTreeHash: 'tree-seal',
  PhysicalUserMessageId: 'msg-seal',
  SealDigest: 'digest-1',
  CanonicalVersion: 3,
  IncludedToolResultDigests: ['tool-1', 'tool-2'],
})

test('WHAT[REVIEW-ASSURANCE-007] SHARED_pending_seal_record_carries_the_binding_candidate', () => {
  assert.deepEqual(sealValue, {
    SessionId: 'ses_seal',
    ManagerSessionId: 'ses_manager',
    BarrierId: 'bar_seal',
    GitTreeHash: 'tree-seal',
    PhysicalUserMessageId: 'msg-seal',
    SealDigest: 'digest-1',
    CanonicalVersion: 3,
    IncludedToolResultDigests: ['tool-1', 'tool-2'],
  })

  const key = 'shared-test-pending-seal'
  try {
    review.setPendingSeal(key, sealValue)
    assert.deepEqual(review.tryGetPendingSeal(key), sealValue, 'the parked candidate round-trips through the shared map')
  } finally {
    review.deletePendingSeal(key)
  }
})
