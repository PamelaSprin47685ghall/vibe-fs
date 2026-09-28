import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import { rootFor } from './support/authority.mjs'

test('WHAT[interaction-authority-013] accepted continuation preserves complete identity evidence within the exact run', () => {
  const root = rootFor()
  let state = authority.registerAuthority(root, authority.empty)
  const claim = authority.claimContinuation('continuation', root.session, 'DegenerationGuard', root, 'payload-digest')
  state = authority.registerClaim(claim, state)
  assert.deepEqual(state.activeLogicalRun, root)
  state = authority.acceptClaim('continuation', 'next-physical-message', state)
  assert.deepEqual(state.activeLogicalRun, root)
  assert.deepEqual(state.acceptedDispatches[0].identitySeed, root.identitySeed)
  assert.equal(state.acceptedContinuations[0].physical, 'next-physical-message')
  const differentRun = rootFor('engineer', 'different-root-message', root.session)
  assert.notEqual(differentRun.logicalRun, root.logicalRun)
  assert.equal(authority.registerAuthority(differentRun, state).ok, false)
})
