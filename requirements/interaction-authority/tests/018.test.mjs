import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  coder: 'Coder',
  manager: 'Lead',
}
const rootSelection = (agent) => {
  const role = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: agent,
      role,
      selectedTier: 'deep',
      persona: personas[agent] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const rootFor = (agent = 'coder', physical = 'msg_u1') => {
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', physical, rootSelection(agent))
  assert.equal(result.ok, true, result.error)
  return result.value
}
const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  participant: value.participantIdentity.participant,
  role: value.participantIdentity.role,
})
const register = (root) => authority.registerAuthority(root, authority.empty)
const continuation = (key, root, kind = 'ManagerGuard', payload = 'payload') =>
  authority.claimContinuation(key, 'ses_a', kind, root, payload)

test('WHAT[INTERACTION-AUTHORITY-018] IA_018_exact_closure_releases_run_scoped_authority_before_root_reuse', () => {
  const first = rootFor()
  let state = register(first)
  const claim = continuation('pk_1', first)
  state = authority.registerClaim(claim, state)
  state = authority.acceptClaim('pk_1', 'msg_c1', state)
  assert.equal(state.claimSequences.length, 1)
  assert.equal(state.acceptedContinuations.length, 1)

  const second = rootFor('manager', 'msg_u2')
  const premature = authority.registerAuthority(second, state)
  assert.equal(premature.ok, false)
  assert.equal(premature.error.kind, 'ActiveRunIdentityConflict')
  assert.equal(premature.error.active.logicalRun, first.logicalRun)
  assert.equal(premature.error.requested.logicalRun, second.logicalRun)

  const closed = authority.closeAuthority(first.logicalRun, first.authorityRoot, state)
  assert.equal(closed.ok, true, closed.ok ? '' : closed.error)
  const after = authority.registerAuthority(second, closed.value)
  assert.deepEqual(profile(after.activeLogicalRun), profile(second))
  assert.deepEqual(profile(after.lastAuthorityProfile), profile(second))
  assert.equal(after.pendingClaims.length, 0)
  assert.equal(after.claimSequences.length, 0)
  assert.equal(after.acceptedContinuations.length, 0)
})
