// Split from tests/unit/host/shared-state.test.mjs (cutover Wave 2a);
// owner: review-assurance. REVIEW-010/013 PendingSeal shape —— parked seal
// candidate 携带 binding candidate（SessionId / ManagerSessionId / BarrierId /
// GitTreeHash / PhysicalUserMessageId / SealDigest / CanonicalVersion /
// IncludedToolResultDigests）并经共享 map round-trip。REVIEW-013 的 frozen
// attempt scope（manager/barrier/tree）随候选冻结，judge 才不被 future barrier
// 重标记。HOST-012 共享面单例断言归 host-boundary（HOST-BOUNDARY-010）。

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  PendingSeal,
  PendingReviewSeals,
} = await import('../../../dist/OpenCode/Host/SharedState.js')
const { sessionId, physicalUser, sealDigest, toList, listItems, reviewBarrierId, gitTreeHash } = await import(
  '../../verification-system/tests/support/domain.mjs'
)

test('SHARED_pending_seal_record_carries_the_binding_candidate', () => {
  const seal = new PendingSeal(
    sessionId('ses_seal'),
    sessionId('ses_manager'),
    reviewBarrierId('bar_seal'),
    gitTreeHash('tree-seal'),
    physicalUser('msg-seal'),
    sealDigest('digest-1'),
    3,
    toList([sealDigest('tool-1'), sealDigest('tool-2')]),
  )
  assert.equal(seal.SessionId.fields[0], 'ses_seal')
  assert.equal(seal.ManagerSessionId.fields[0], 'ses_manager')
  assert.equal(seal.BarrierId.fields[0], 'bar_seal')
  assert.equal(seal.GitTreeHash.fields[0], 'tree-seal')
  assert.equal(seal.PhysicalUserMessageId.fields[0], 'msg-seal')
  assert.equal(seal.SealDigest.fields[0], 'digest-1')
  assert.equal(seal.CanonicalVersion, 3)
  assert.deepEqual(
    listItems(seal.IncludedToolResultDigests).map((d) => d.fields[0]),
    ['tool-1', 'tool-2'],
  )

  const key = 'shared-test-pending-seal'
  try {
    PendingReviewSeals.set(key, seal)
    assert.equal(PendingReviewSeals.get(key), seal, 'the parked candidate round-trips through the shared map')
  } finally {
    PendingReviewSeals.delete(key)
  }
})
