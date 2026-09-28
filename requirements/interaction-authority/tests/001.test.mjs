import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withJournal, acceptOwner, hostPort } from './support/authority.mjs'

test('WHAT[interaction-authority-001] receipt stays pending across reopen and only exact physical acceptance creates child authority', async () => {
  await withJournal('physical-boundary', async (handle, reopen) => {
    const owner = await acceptOwner(handle)
    const seed = authority.issueInheritedIdentitySeed('engineer', owner)
    assert.equal(seed.ok, true)
    const sent = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => dispatch.admittedWithReceipt('msg-looks-physical')),
      handle, 'child', 'a bounded assignment', seed.value,
    )
    assert.equal(sent.ok, true, sent.error)
    const pending = dispatch.projectionObservation(handle, 'child')
    assert.equal(pending.activeLogicalRun, null)
    assert.equal(pending.pendingClaims.length, 1)
    handle = await reopen()
    assert.equal(dispatch.projectionObservation(handle, 'child').activeLogicalRun, null)
    const accepted = await dispatch.acceptAgentOwnerRoot(handle, 'child', sent.key, 'actual-physical-message')
    assert.equal(accepted.ok, true, accepted.error)
    assert.equal(accepted.profile.authorityRoot, 'actual-physical-message')
    assert.deepEqual(accepted.profile.identitySeed, seed.value)
    assert.deepEqual(dispatch.projectionObservation(handle, 'child').activeLogicalRun, accepted.profile)
  })
})

test.todo('WHAT[interaction-authority-001] GAP-122 compiler rejects using a TransportReceipt where authority promotion requires proven physical identity')
