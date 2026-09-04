// INTERACTION-AUTHORITY package proof — root provenance and run reset.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
  devops: 'Operator',
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
  selectedAgent: value.participantIdentity.selectedAgent,
  peerAgent: value.participantIdentity.peerAgent,
  canonicalRole: value.participantIdentity.canonicalRole,
  selectedTier: value.participantIdentity.selectedTier,
})

const register = (root) => authority.registerAuthority(root, authority.empty)
const continuation = (key, root, kind = 'ManagerGuard', agent = 'coder', payload = 'payload') =>
  authority.claimContinuation(key, 'ses_a', kind, root, agent, payload)

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_malformed_profile_role_and_root_kind_fail_closed_tier_is_compat', () => {
  const root = rootFor()
  const identity = root.identitySeed.participantIdentity
  const malformed = [
    [{ ...root, identitySeed: { ...root.identitySeed, participantIdentity: { ...identity, canonicalRole: 'unknown' } } }, /unknown role/],
    [{ ...root, authorityKind: 'unknown' }, /unknown authority root kind/],
  ]
  // selectedTier is a compat view field: unknown values are normalized to deep, not rejected.
  const tierCompat = { ...root, identitySeed: { ...root.identitySeed, participantIdentity: { ...identity, selectedTier: 'unknown' } } }
  const tierResult = authority.registerAuthority(tierCompat, authority.empty)
  assert.equal(tierResult.activeLogicalRun.participantIdentity.selectedTier, 'deep')
  for (const [candidate, expected] of malformed) {
    const result = authority.registerAuthority(candidate, authority.empty)
    assert.equal(result.ok, false)
    assert.match(result.error, expected)
  }
})

// INTERACTION-AUTHORITY-001: only an explicit physical-message promotion crosses into authority.
test('WHAT[INTERACTION-AUTHORITY-001] IA_001_physical_message_promotes_to_authority_root', () => {
  assert.equal(authority.promotePhysical('msg_u1'), 'msg_u1')
  assert.equal(rootFor().authorityRoot, 'msg_u1')
})

test('WHAT[INTERACTION-AUTHORITY-002] IA_002_transport_receipt_shape_is_not_authority_evidence', () => {
  assert.equal(authority.transportReceiptShape('accepted-1a2b'), true)
  assert.equal(authority.transportReceiptShape('msg_real'), false)
})

// INTERACTION-AUTHORITY-003: the accepted root carries the identity owner's immutable evidence.
test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_carries_resolved_participant_identity', () => {
  assert.deepEqual(profile(rootFor('coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'coder',
    peerAgent: 'coder',
    canonicalRole: 'coder',
    selectedTier: 'deep',
  })
  assert.deepEqual(profile(rootFor('manager')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'manager',
    peerAgent: 'manager',
    canonicalRole: 'manager',
    selectedTier: 'deep',
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_closed_root_replacement_clears_run_scoped_state', () => {
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

// INTERACTION-AUTHORITY-006: canonical bare names resolve; legacy and hyphenated names fail closed.
test('WHAT[INTERACTION-AUTHORITY-006] IA_006_canonical_names_resolve_and_legacy_or_malformed_are_refused', () => {
  for (const name of ['coder', 'manager', 'inspector']) {
    const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', 'msg_u1', rootSelection(name))
    assert.equal(result.ok, true, result.error)
    assert.equal(authority.parseAgentName(name).ok, true)
  }
  for (const name of ['build', 'plan', 'student', 'teacher', 'meditator', 'executor', 'fast_coder']) {
    assert.equal(authority.parseAgentName(name).error.kind, 'LegacyAgentName')
    const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', 'msg_u1', rootSelection(name))
    assert.equal(result.ok, false)
  }
  assert.equal(authority.parseAgentName('nonsense').error.kind, 'UnknownManagedAgent')
  assert.equal(authority.parseAgentName('fast-').error.kind, 'Malformed')
  assert.equal(authority.parseAgentName('fast-coder').error.kind, 'Malformed')
  assert.equal(authority.parseAgentName('Coder').error.kind, 'Malformed')
})

test('WHAT[INTERACTION-AUTHORITY-006] IA_006_agent_owner_root_claim_rejects_legacy_name', () => {
  const inherited = authority.issueInheritedIdentitySeed('build', rootFor('manager'))
  assert.equal(inherited.ok, false)
  assert.match(inherited.error, /legacy|managed|malformed/i)
})

// INTERACTION-AUTHORITY-010: repair identity is durable and bounded by its occasion.
test('WHAT[INTERACTION-AUTHORITY-010] IA_010_terminal_repair_identity_is_exactly_once', () => {
  const root = rootFor()
  let state = register(root)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), false)

  const repair = authority.claimContinuation(
    'pk_rep',
    'ses_a',
    'InteractionRepair',
    root,
    'coder',
    authority.repairPayloadDigest('req-empty', 'run_term', 'empty'),
  )
  state = authority.registerClaim(repair, state)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), true)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run-other', 'empty', state), false)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'xml-only', state), false)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-next', 'run_term', 'empty', state), false)

  state = authority.abandonClaim('pk_rep', state)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), true)
})

test('WHAT[INTERACTION-AUTHORITY-016] IA_016_agent_owner_root_has_no_run_before_physical_acceptance', () => {
  const owner = rootFor('manager')
  const inherited = authority.issueInheritedIdentitySeed('manager', owner)
  assert.equal(inherited.ok, true, inherited.error)
  const claim = authority.claimAgentOwnerRoot('pk_owner', 'ses_a', 'pd-owner', inherited.value)
  assert.equal(claim.ok, true, claim.error)
  assert.deepEqual(
    {
      origin: claim.value.origin,
      label: claim.value.originLabel,
      hasRun: claim.value.logicalRun !== null,
      hasRoot: claim.value.authorityRoot !== null,
      effectiveAgent: claim.value.effectiveAgent,
    },
    { origin: 'AuthorityRoot', label: 'AgentOwnerRoot', hasRun: false, hasRoot: false, effectiveAgent: 'manager' },
  )

  let state = authority.registerClaim(claim.value, register(owner))
  state = authority.acceptClaim('pk_owner', 'msg_owner', state)
  assert.equal(state.pendingClaims.length, 0)
  assert.equal(state.acceptedContinuations.length, 0)
  assert.equal(authority.resolveKnownOrigin('msg_owner', '', false, state), 'UnknownOrigin')
})

test('WHAT[INTERACTION-AUTHORITY-011] PROMPT_011_logical_run_id_is_stable_and_input_sensitive', () => {
  const id = (runtime, session, physical) => authority.stableLogicalRunId(hash, runtime, session, physical)
  const base = id('rt_1', 'ses_a', 'msg_u1')
  assert.equal(base, 'H(rt_1\nses_a\nmsg_u1)')
  assert.equal(id('rt_1', 'ses_a', 'msg_u1'), base)
  assert.notEqual(id('rt_2', 'ses_a', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_b', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_a', 'msg_u2'), base)
})

test('WHAT[INTERACTION-AUTHORITY-012] IA_005_degeneration_guard_is_continuation', () => {
  for (const kind of ['DegenerationGuard', 'ManagerGuard']) {
    assert.deepEqual(authority.originForContinuation(kind), { kind: 'Continuation', label: kind })
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})

test('WHAT[INTERACTION-AUTHORITY-013] continuation preserves logical run and root authority profile', () => {
  const root = rootFor()
  const state = authority.registerClaim(continuation('pk_c', root, 'DegenerationGuard', 'coder', 'pd-n'), register(root))
  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_remains_the_source_for_continuations', () => {
  const root = rootFor()
  const state = authority.registerClaim(continuation('pk_c', root, 'BusyAgentNudge', 'coder', 'pd-n'), register(root))
  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
  assert.deepEqual(profile(state.lastAuthorityProfile), profile(root))
})
