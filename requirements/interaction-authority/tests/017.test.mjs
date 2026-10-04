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
    assert.equal(result.observation, null)
    assert.equal(dispatch.projectionObservation(handle, 'never-active-target').pendingClaims.length, 0)
  })
})

test('WHAT[interaction-authority-017] dispatcher rejects a continuation whose supplied profile names another logical run', async () => {
  await withJournal('wrong-run-target', async (handle) => {
    const owner = await acceptOwner(handle, 'wrong-run-target')
    const wrongRun = { ...owner, logicalRun: `${owner.logicalRun}-other` }
    let sends = 0
    const result = await dispatch.sendContinuation(hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt('receipt')
    }), handle, 'wrong-run-target', 'continue', 'ManagerGuard', wrongRun, 'Await')
    // The target has an active run, but the supplied profile does not name it:
    // a profile is a claim, not authority, so it is refused before any claim
    // is written or the Host transport is reached.
    assert.equal(result.ok, false)
    assert.match(result.error, /does not match the active logical run/)
    assert.match(result.error, new RegExp(`${owner.logicalRun}-other`))
    assert.equal(sends, 0)
    assert.equal(result.observation, null)
    assert.equal(dispatch.projectionObservation(handle, 'wrong-run-target').pendingClaims.length, 0)
    assert.equal(dispatch.projectionObservation(handle, 'wrong-run-target').claimSequences.length, 0)
  })
})

test('WHAT[interaction-authority-017] dispatcher rejects a continuation whose supplied profile names another authority root', async () => {
  await withJournal('wrong-root-target', async (handle) => {
    const owner = await acceptOwner(handle, 'wrong-root-target')
    const wrongRoot = { ...owner, authorityRoot: `${owner.authorityRoot}-other` }
    let sends = 0
    const result = await dispatch.sendContinuation(hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt('receipt')
    }), handle, 'wrong-root-target', 'continue', 'ManagerGuard', wrongRoot, 'Await')
    assert.equal(result.ok, false)
    assert.match(result.error, /does not match the active logical run/)
    assert.equal(sends, 0)
    assert.equal(result.observation, null)
    assert.equal(dispatch.projectionObservation(handle, 'wrong-root-target').pendingClaims.length, 0)
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
    // PROMPT-002/004: a transport receipt is only `Submitted`; the claim leaves
    // the pending map only when a real physical message id resolves it, same as
    // the root claim was resolved by acceptAgentOwnerRoot above.
    const landed = await dispatch.acceptManagedPromptClaim(handle, 'active-target', 'physical-continuation-active', continuation.key, 'engineer')
    assert.equal(landed.ok, true, landed.error)
    const observation = dispatch.projectionObservation(handle, 'active-target')
    assert.equal(observation.pendingClaims.length, 0)
    // PROMPT-011: ClaimSequences count within one logical run, and accepting
    // the root (registerAuthority) restarts the count, so the durable
    // projection keeps only the continuation's scope, at sequence 1.
    assert.equal(observation.claimSequences.length, 1)
    assert.equal(observation.claimSequences[0].count, 1)
  })
})

// Closed-target counterexample (run closed, LastAuthorityProfile retained) needs
// a real durable closure fact: the Relay RetirementCommitted transaction that
// folds closeCompletedHumanRootManager. That witness belongs to
// interaction-authority-018/GAP-123 lifecycle closure work and is tracked there.
test.todo('WHAT[interaction-authority-017] dispatcher rejects a continuation whose target run is durably closed while its archived profile is still readable')
