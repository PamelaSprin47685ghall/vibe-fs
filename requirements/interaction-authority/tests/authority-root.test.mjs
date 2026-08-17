// INTERACTION-AUTHORITY package proof — root provenance and run reset.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const rootFor = (agent = 'fast-coder', physical = 'msg_u1', kind = 'HumanRoot') => {
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', kind, physical, agent)
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
  canonicalRole: value.canonicalRole,
  selectedTier: value.selectedTier,
})

const register = (root) => authority.registerAuthority(root, authority.empty)
const continuation = (key, root, kind = 'ManagerGuard', agent = 'fast-coder', payload = 'payload') =>
  authority.claimContinuation(key, 'ses_a', kind, root, agent, payload)

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_malformed_profile_role_tier_and_root_kind_fail_closed', () => {
  const root = rootFor()
  for (const field of ['canonicalRole', 'selectedTier', 'authorityKind']) {
    const result = authority.registerAuthority({ ...root, [field]: 'unknown' }, authority.empty)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|tier|authority root kind)/)
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

// INTERACTION-AUTHORITY-003: the root fixes the complete immutable profile.
test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_derives_peer_role_and_tier_from_selected_agent', () => {
  assert.deepEqual(profile(rootFor('fast-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'fast-coder',
    peerAgent: 'deep-coder',
    canonicalRole: 'coder',
    selectedTier: 'fast',
  })
  assert.deepEqual(profile(rootFor('deep-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'deep-coder',
    peerAgent: 'fast-coder',
    canonicalRole: 'coder',
    selectedTier: 'deep',
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_new_root_clears_run_scoped_state', () => {
  const first = rootFor()
  let state = register(first)
  const claim = continuation('pk_1', first)
  state = authority.registerClaim(claim, state)
  state = authority.acceptClaim('pk_1', 'msg_c1', state)
  assert.equal(state.claimSequences.length, 1)
  assert.equal(state.acceptedContinuations.length, 1)

  const second = rootFor('deep-reviewer', 'msg_u2')
  const after = authority.registerAuthority(second, state)
  assert.deepEqual(profile(after.activeLogicalRun), profile(second))
  assert.deepEqual(profile(after.lastAuthorityProfile), profile(second))
  assert.equal(after.pendingClaims.length, 0)
  assert.equal(after.claimSequences.length, 0)
  assert.equal(after.acceptedContinuations.length, 0)
})

// INTERACTION-AUTHORITY-006: unqualified/legacy names fail closed with typed reasons.
test('WHAT[INTERACTION-AUTHORITY-006] IA_006_bare_and_unknown_agent_names_are_refused', () => {
  for (const name of ['coder', 'manager', 'reviewer']) {
    const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', 'msg_u1', name)
    assert.equal(result.ok, false)
  }
  assert.equal(authority.parseAgentName('coder').error.kind, 'LegacyAgentName')
  assert.equal(authority.parseAgentName('nonsense').error.kind, 'Malformed')
  assert.equal(authority.parseAgentName('unknown-role').error.kind, 'UnknownManagedAgent')
})

test('WHAT[INTERACTION-AUTHORITY-006] IA_006_agent_owner_root_claim_rejects_legacy_name', () => {
  const claim = authority.claimAgentOwnerRoot('pk_b', 'ses_a', 'pd', 'manager')
  assert.equal(claim.ok, false)
  assert.match(claim.error, /legacy|managed|fast-\*|deep-\*/i)
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
    'fast-coder',
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
  const claim = authority.claimAgentOwnerRoot('pk_owner', 'ses_a', 'pd-owner', 'fast-manager')
  assert.equal(claim.ok, true, claim.error)
  assert.deepEqual(
    {
      origin: claim.value.origin,
      label: claim.value.originLabel,
      hasRun: claim.value.logicalRun !== null,
      hasRoot: claim.value.authorityRoot !== null,
      effectiveAgent: claim.value.effectiveAgent,
    },
    { origin: 'AuthorityRoot', label: 'AgentOwnerRoot', hasRun: false, hasRoot: false, effectiveAgent: 'fast-manager' },
  )

  let state = authority.registerClaim(claim.value, authority.empty)
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

test('WHAT[INTERACTION-AUTHORITY-012] IA_005_needhelp_kinds_are_continuations', () => {
  for (const kind of ['NeedHelpEscalation', 'NeedHelpAdvice']) {
    assert.deepEqual(authority.originForContinuation(kind), { kind: 'Continuation', label: kind })
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), null)
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_remains_the_source_for_continuations', () => {
  const root = rootFor()
  const state = authority.registerClaim(continuation('pk_c', root, 'BusyAgentNudge', 'deep-coder', 'pd-n'), register(root))
  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
  assert.deepEqual(profile(state.lastAuthorityProfile), profile(root))
})
