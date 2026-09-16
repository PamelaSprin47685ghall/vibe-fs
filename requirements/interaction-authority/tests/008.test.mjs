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
const inheritedSelection = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    hash,
    'rt_owner',
    'ses_owner',
    'HumanRoot',
    `owner_${physical}`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const rootFor = (agent = 'coder', physical = 'msg_u1', kind = 'HumanRoot') => {
  const seed = kind === 'AgentOwnerRoot' ? inheritedSelection(agent, physical) : rootSelection(agent)
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', kind, physical, seed)
  assert.equal(result.ok, true, result.error)
  return result.value
}
const register = (root) => authority.registerAuthority(root, authority.empty)

test('WHAT[INTERACTION-AUTHORITY-008] IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root', () => {
  const root = rootFor('coder', 'msg_u1', 'AgentOwnerRoot')
  let state = register(root)

  const claimed = authority.claimContinuation('pk_claimed', 'ses_a', 'ManagerGuard', root, 'pd-c')
  state = authority.registerClaim(claimed, state)
  const accepted = authority.claimContinuation('pk_accepted', 'ses_a', 'BusyAgentNudge', root, 'pd-a')
  state = authority.registerClaim(accepted, state)
  state = authority.acceptClaim('pk_accepted', 'msg_accepted', state)

  assert.deepEqual(
    {
      accepted: authority.resolveKnownOrigin('msg_accepted', '', false, state),
      claimed: authority.resolveKnownOrigin('msg_unseen', 'pk_claimed', false, state),
      compaction: authority.resolveKnownOrigin('msg_unseen', '', true, state),
      registeredRoot: authority.resolveKnownOrigin('msg_unseen', 'pk_unknown', false, state),
      nothing: authority.resolveKnownOrigin('msg_unseen', '', false, state),
    },
    {
      accepted: 'Continuation',
      claimed: 'Continuation',
      compaction: 'HostInternal',
      registeredRoot: 'AuthorityRoot',
      nothing: 'UnknownOrigin',
    },
  )
})

test('WHAT[INTERACTION-AUTHORITY-008] IA_008_accepted_continuation_outranks_compaction', () => {
  const root = rootFor()
  let state = register(root)
  state = authority.registerClaim(
    authority.claimContinuation('pk_both', 'ses_a', 'ManagerGuard', root, 'pd-b'),
    state,
  )
  state = authority.acceptClaim('pk_both', 'msg_both', state)
  assert.equal(authority.resolveKnownOrigin('msg_both', '', true, state), 'Continuation')
})
