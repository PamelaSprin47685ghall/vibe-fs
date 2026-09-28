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

test('WHAT[interaction-authority-017] dispatcher rejects a continuation with no active run at its target', { todo: 'GAP-125 actual sender accepts an externally supplied profile without active target validation' }, async () => {
  await withJournal('no-active-target', async (handle) => {
    const owner = await acceptOwner(handle)
    let sends = 0
    const result = await dispatch.sendContinuation(hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt('receipt')
    }), handle, 'never-active-target', 'continue', 'ManagerGuard', owner, 'Await')
    assert.equal(result.ok, false)
    assert.equal(sends, 0)
    assert.equal(dispatch.projectionObservation(handle, 'never-active-target').pendingClaims.length, 0)
  })
})
