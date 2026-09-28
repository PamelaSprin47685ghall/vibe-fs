import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withJournal, acceptOwner, hostPort } from './support/authority.mjs'

for (const receipt of ['accepted-queue-entry', 'msg-looks-like-physical']) {
  test(`WHAT[dispatch-protocol-003] typed transport receipt ${receipt} remains pending and grants no authority after journal reopen`, async () => {
    await withJournal(`receipt-${receipt}`, async (handle, reopen) => {
      const seed = authority.issueInheritedIdentitySeed('engineer', await acceptOwner(handle))
      assert.equal(seed.ok, true)
      const sent = await dispatch.sendAgentOwnerRootAwait(hostPort(async () => dispatch.admittedWithReceipt(receipt)), handle, 'child', 'assignment', seed.value)
      assert.equal(sent.ok, true, sent.error)
      handle = await reopen()
      const projection = dispatch.projectionObservation(handle, 'child')
      assert.equal(projection.activeLogicalRun, null)
      assert.equal(projection.pendingClaims.length, 1)
      assert.equal(projection.pendingClaims[0].promptKey, sent.key)
      assert.equal(projection.pendingClaims[0].receipt, receipt)
    })
  })
}
