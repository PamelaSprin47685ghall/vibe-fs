import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { rootFor, withJournal, acceptOwner, hostPort } from './support/authority.mjs'

test('WHAT[interaction-authority-017] real pure close removes previously accepted continuation lookup', () => {
  const root = rootFor()
  let state = authority.registerAuthority(root, authority.empty)
  state = authority.registerClaim(authority.claimContinuation('claimed', root.session, 'ManagerGuard', root, 'digest'), state)
  state = authority.acceptClaim('claimed', 'accepted-continuation', state)
  assert.equal(authority.resolveKnownOrigin('accepted-continuation', '', false, state), 'Continuation')
  const closed = authority.closeAuthority(root.logicalRun, root.authorityRoot, state)
  assert.equal(closed.ok, true)
  assert.equal(closed.value.lastAuthorityProfile.logicalRun, root.logicalRun)
  assert.equal(authority.resolveKnownOrigin('accepted-continuation', 'claimed', false, closed.value), 'UnknownOrigin')
})

test('WHAT[interaction-authority-017] dispatcher rejects a continuation with no active run at its target', async () => {
  await withJournal('no-active-target', async (handle) => {
    const owner = await acceptOwner(handle)
    let sends = 0
    const result = await dispatch.sendContinuation(hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt('receipt')
    }), handle, 'never-active-target', 'continue', 'ManagerGuard', owner, 'Await')
    assert.equal(result.ok, false)
    assert.equal(result.error, 'No active authority profile')
    // A rejected target must never reach the Host transport and must leave no
    // durable claim behind: an externally supplied profile is not authority.
    assert.equal(sends, 0)
    assert.equal(dispatch.projectionObservation(handle, 'never-active-target').pendingClaims.length, 0)
  })
})

test('WHAT[interaction-authority-017] dispatcher delivers a continuation to a target that has an active run', async () => {
  await withJournal('active-target', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = authority.issueInheritedIdentitySeed('engineer', owner)
    assert.equal(seed.ok, true, seed.ok ? '' : seed.error)
    const sentKeys = []
    const port = hostPort(async (session, text, options) => {
      sentKeys.push(options.Metadata.wanxiangshu_prompt_key)
      return dispatch.admittedWithReceipt('accepted-active')
    })
    const root = await dispatch.sendAgentOwnerRootAwait(port, handle, 'active-target', 'bounded assignment', seed.value)
    assert.equal(root.ok, true, root.error)
    const accepted = await dispatch.acceptAgentOwnerRoot(handle, 'active-target', root.key, 'physical-root-active')
    assert.equal(accepted.ok, true, accepted.error)
    const continuation = await dispatch.sendContinuation(port, handle, 'active-target', 'continue same work', 'ManagerGuard', accepted.profile, 'Await')
    // The same gate must admit the legitimate continuation: a sender that
    // rejected everything would satisfy the test above and lose real work.
    assert.equal(continuation.ok, true, continuation.error)
    assert.deepEqual(sentKeys, [root.key, continuation.key])
  })
})
