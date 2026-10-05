import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withJournal, acceptOwner, hostPort } from './support/authority.mjs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import { withSyncRuntime } from '../../delegation/tests/support/sync-runtime.mjs'

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

test('WHAT[dispatch-protocol-003] observed receipt-only send stays unaccepted until true physical ingress while preserving the original receipt carrier', async () => {
  assert.equal(typeof sync.startObserved, 'function', 'new observed API missing; capability red, not an existing business failure')
  const owner = 'observed-receipt-owner'
  await withSyncRuntime(owner, async runtime => {
    const execution = sync.startObserved(runtime, owner, 'RECEIPT-CHARGE')
    const admission = sync.observedAdmission(execution)
    const completion = sync.observedCompletion(execution)
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    const sent = sync.promptIdentity(runtime, owner, 'Engineer', 0)
    assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 0, dispatch.admittedWithReceipt('msg-shaped-transport-receipt')), true)
    const turn = () => new Promise(resolve => setImmediate(resolve))
    await turn()
    assert.equal(await Promise.race([admission.then(() => 'settled'), turn().then(() => 'pending')]), 'pending')
    const pending = sync.promptClaimState(runtime, owner, 'Engineer', 0)
    assert.equal(pending.kind, 'Pending')
    assert.equal(pending.promptKey, sent.promptKey)
    assert.equal(await sync.confirmPromptPhysical(runtime, owner, 'Engineer', 0, 'actual-ingress-physical'), true)
    const accepted = await admission
    assert.equal(accepted.kind, 'Accepted')
    assert.equal(accepted.promptKey, sent.promptKey)
    assert.deepEqual(accepted.hostOutcome, { kind: 'AdmittedWithReceipt', value: 'msg-shaped-transport-receipt' })
    assert.equal(accepted.physicalUserMessageId, 'actual-ingress-physical')
    assert.notEqual(accepted.physicalUserMessageId, accepted.hostOutcome.value)
    assert.equal(sync.promptClaimState(runtime, owner, 'Engineer', 0).kind, 'Accepted')
    assert.equal(await sync.settleExactTerminal(runtime, accepted.sessionId, accepted.physicalUserMessageId, accepted.authorityRootUserMessageId, 'receipt-provider-run', 'RECEIPT-FORMAL', ''), true)
    assert.equal((await completion).value.providerRun, 'receipt-provider-run')
    assert.equal(sync.promptCount(runtime, owner, 'Engineer'), 1)
    assert.equal(sync.terminalListenerCount(runtime), 0)
  })
})
