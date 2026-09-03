// INTERACTION-AUTHORITY package proof — continuation provenance and ingress precedence.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as intent from '../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
}
const rootSelection = (agent) => {
  const canonicalRole = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: agent,
      peerAgent: agent,
      canonicalRole,
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
const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  selectedAgent: value.selectedAgent,
  peerAgent: value.peerAgent,
})
const register = (root) => authority.registerAuthority(root, authority.empty)

// INTERACTION-AUTHORITY-004: a continuation inherits run/root and changes only effective agent.
test('WHAT[INTERACTION-AUTHORITY-004] IA_004_continuation_inherits_run_and_root', () => {
  const root = rootFor()
  const before = register(root)
  const claim = authority.claimContinuation('pk_c', 'ses_a', 'ProviderRetryAttempt', root, 'coder', 'pd-retry')

  assert.deepEqual(
    {
      origin: claim.origin,
      logicalRun: claim.logicalRun,
      authorityRoot: claim.authorityRoot,
      effectiveAgent: claim.effectiveAgent,
    },
    {
      origin: 'Continuation',
      logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
      authorityRoot: 'msg_u1',
      effectiveAgent: 'coder',
    },
  )

  const after = authority.registerClaim(claim, before)
  assert.deepEqual(profile(after.activeLogicalRun), profile(root))
  assert.deepEqual(profile(after.lastAuthorityProfile), profile(root))
})

test('WHAT[INTERACTION-AUTHORITY-005] IA_005_every_continuation_kind_is_parseable_and_not_root', () => {
  const kinds = [
    'InteractionRepair',
    'JoinGuard',
    'ManagerGuard',
    'ReviewerGuard',
    'BusyAgentNudge',
    'ProviderRetryAttempt',
    'DegenerationGuard',
    'ManagerIdleEncouragement',
    'FinalityRejected',
    'FinalitySteer',
  ]

  for (const kind of kinds) {
    assert.deepEqual(authority.originForContinuation(kind), { kind: 'Continuation', label: kind })
    assert.deepEqual(authority.tryParseContinuationKind(kind), { kind })
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})

// INTERACTION-AUTHORITY-008/009: accepted > claimed > compaction > owner root > unknown.
test('WHAT[INTERACTION-AUTHORITY-008] IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root', () => {
  const root = rootFor('coder', 'msg_u1', 'AgentOwnerRoot')
  let state = register(root)

  const claimed = authority.claimContinuation('pk_claimed', 'ses_a', 'ReviewerGuard', root, 'coder', 'pd-c')
  state = authority.registerClaim(claimed, state)
  const accepted = authority.claimContinuation('pk_accepted', 'ses_a', 'BusyAgentNudge', root, 'coder', 'pd-a')
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
    authority.claimContinuation('pk_both', 'ses_a', 'ManagerGuard', root, 'coder', 'pd-b'),
    state,
  )
  state = authority.acceptClaim('pk_both', 'msg_both', state)
  assert.equal(authority.resolveKnownOrigin('msg_both', '', true, state), 'Continuation')
})

test('WHAT[INTERACTION-AUTHORITY-009] IA_009_pure_resolution_never_infers_human_root', () => {
  const root = rootFor('coder', 'msg_u1', 'HumanRoot')
  const state = register(root)
  assert.equal(state.activeLogicalRun.authorityKind, 'HumanRoot')
  assert.equal(authority.resolveKnownOrigin('msg_new', 'pk_any', false, state), 'UnknownOrigin')
})

test('WHAT[INTERACTION-AUTHORITY-016] IA_016_accepted_root_claim_stays_out_of_continuation_map', () => {
  const claim = authority.claimAgentOwnerRoot(
    'pk_owner',
    'ses_a',
    'pd-owner',
    inheritedSelection('manager', 'msg_owner'),
  )
  assert.equal(claim.ok, true, claim.error)
  let state = authority.registerClaim(claim.value, authority.empty)
  state = authority.acceptClaim('pk_owner', 'msg_owner', state)
  assert.equal(state.pendingClaims.length, 0)
  assert.equal(state.acceptedContinuations.length, 0)
  assert.equal(authority.resolveKnownOrigin('msg_owner', '', false, state), 'UnknownOrigin')
})

test('WHAT[INTERACTION-AUTHORITY-007] IA_007_unknown_origin_changes_no_projection_state', () => {
  const root = rootFor()
  const state = register(root)
  const before = JSON.stringify(state)
  assert.equal(authority.resolveKnownOrigin('msg_never_proven', 'pk_never_proven', false, state), 'UnknownOrigin')
  assert.deepEqual(intent.resolve({ sessionId: 'ses_a', physicalUserMessageId: 'msg_never_proven', explicitAgent: null, promptKey: null, hostCompaction: false, hostSynthetic: false }, { available: true, activeAgent: 'coder', activeKind: 'HumanRoot', claims: [], acceptedContinuations: [] }), { case: 'Reject', reason: 'UnknownOriginWhileActive' })
  assert.equal(JSON.stringify(state), before)
})

test('WHAT[INTERACTION-AUTHORITY-017] IA_017_claimed_key_without_active_run_stays_unknown', () => {
  const root = rootFor()
  const state = authority.registerAuthority(root, authority.empty)
  const closed = { ...state, activeLogicalRun: null }
  assert.equal(authority.resolveKnownOrigin('msg_x', 'pk_never_claimed', false, closed), 'UnknownOrigin')
})
