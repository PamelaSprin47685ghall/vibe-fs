import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import { rootFor } from './support/authority.mjs'

test('WHAT[interaction-authority-004] each current continuation retains the complete run root and identity evidence', () => {
  const root = rootFor()
  const before = authority.registerAuthority(root, authority.empty)
  for (const kind of ['InteractionRepair', 'JoinGuard', 'ManagerGuard', 'BusyAgentNudge', 'HumanMessage', 'ManagedDelegationAssignment', 'ProviderRetryAttempt', 'DegenerationGuard', 'FissionHandoff']) {
    const claim = authority.claimContinuation(`claim-${kind}`, root.session, kind, root, `digest-${kind}`)
    assert.equal(claim.origin, 'Continuation')
    assert.equal(claim.logicalRun, root.logicalRun)
    assert.equal(claim.authorityRoot, root.authorityRoot)
    assert.deepEqual(claim.identitySeed, root.identitySeed)
    const after = authority.registerClaim(claim, before)
    assert.deepEqual(after.activeLogicalRun, root)
    assert.deepEqual(after.lastAuthorityProfile, root)
  }
})

test.todo('WHAT[interaction-authority-004] GAP-122 actual continuation never resets fallback or repair budget while changing only an authorized physical binding')
