import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withJournal, acceptOwner, hostPort } from './support/authority.mjs'

test('WHAT[dispatch-protocol-001] actual owner-root and continuation sends reach Host with an already registered exact claim', async () => {
  await withJournal('dispatch-entry', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = authority.issueInheritedIdentitySeed('engineer', owner)
    assert.equal(seed.ok, true)
    const sentKeys = []
    const port = hostPort(async (session, text, options) => {
      const key = options.Metadata.wanxiangshu_prompt_key
      const claim = dispatch.projectionObservation(handle, session).pendingClaims.find(value => value.promptKey === key)
      assert.ok(claim, 'Host must not observe an unregistered send')
      assert.equal(claim.identitySeed.participantIdentity.participant, 'engineer')
      sentKeys.push(key)
      return dispatch.admittedWithReceipt('accepted-entry')
    })
    const root = await dispatch.sendAgentOwnerRootAwait(port, handle, 'child', 'bounded assignment', seed.value)
    assert.equal(root.ok, true, root.error)
    const accepted = await dispatch.acceptAgentOwnerRoot(handle, 'child', root.key, 'physical-root')
    assert.equal(accepted.ok, true, accepted.error)
    const continuation = await dispatch.sendContinuation(port, handle, 'child', 'continue same work', 'ProviderRetryAttempt', accepted.profile, 'Await')
    assert.equal(continuation.ok, true, continuation.error)
    assert.deepEqual(sentKeys, [root.key, continuation.key])
    assert.notEqual(root.key, continuation.key)
  })
})

test.todo('WHAT[dispatch-protocol-001] every actual synthetic-message producer uses the registered dispatcher, including Guard, repair, nudge and Orchestrator paths (GAP-136)')
