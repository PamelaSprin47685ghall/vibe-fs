// DISPATCH-PROTOCOL package proof — claim lifecycle and deterministic dispatch identity.
//
// PROMPT-005 four-state lifecycle, transport receipt shape, PromptKey identity,
// ClaimSequence registration, runtime-start audit stamps, and the single
// PromptDispatcher writer are observed through production JSON surfaces.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const H = (input) => `H(${input})`
const RUNTIME = 'rt_1'
const SESSION = 'ses_a'

const findClaim = (projection, key) => projection.pendingClaims.find((claim) => claim.promptKey === key)
const promptOrigin = (kind) => authority.originForContinuation(kind)

const personas = {
  coder: 'Coder',
  manager: 'Lead',
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
const inheritedSeed = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    physical,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}

const profileOf = () => {
  const built = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    'msg_u1',
    rootSelection('coder'),
  )
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

// ── DISPATCH-PROTOCOL-002/003: Submitted records receipt but leaves claim pending ──

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_submit_records_the_receipt_without_resolving_the_claim', () => {
  const root = profileOf()
  const key = 'pk_s'
  const claim = authority.claimContinuation(key, SESSION, 'ManagerGuard', root, 'coder', 'pd-1')

  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(claim, projection)
  assert.equal(findClaim(projection, key).receipt, null)

  const submitted = authority.submitClaim(key, 'accepted-9f', projection)
  const stored = findClaim(submitted, key)

  assert.deepEqual(
    {
      pending: submitted.pendingClaims.length,
      receipt: stored.receipt,
    },
    { pending: 1, receipt: 'accepted-9f' },
    'Submitted keeps claim pending: only real chat.message resolves it',
  )
})

// ── DISPATCH-PROTOCOL-002: abandon removes claim without changing active run ──

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone', () => {
  const root = profileOf()
  const key = 'pk_x'
  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'BusyAgentNudge', root, 'coder', 'pd-n'),
    projection,
  )

  const after = authority.abandonClaim(key, projection)

  assert.equal(after.pendingClaims.length, 0)
  assert.equal(after.activeLogicalRun.logicalRun, root.logicalRun)
})

// ── DISPATCH-PROTOCOL-006: abandon consumes sequence ──

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_abandon_keeps_the_claim_sequence_consumed', () => {
  const root = profileOf()
  const key = 'pk_x'
  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'BusyAgentNudge', root, 'coder', 'pd-n'),
    projection,
  )

  const after = authority.abandonClaim(key, projection)

  assert.equal(after.claimSequences.length, 1)
})

// ── DISPATCH-PROTOCOL-003: physical acceptance only has real physical evidence ──

test('WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity', () => {
  const admission = 'accepted-1a2b'
  const physical = 'msg_real'
  assert.equal(admission, 'accepted-1a2b')
  assert.equal(physical, 'msg_real')
  assert.equal(authority.transportReceiptShape(admission), true, 'accepted-* is admission shape')
  assert.equal(authority.transportReceiptShape(physical), false, 'msg_* is not admission shape')
})

// ── DISPATCH-PROTOCOL-005/006: deterministic PromptKey identity ──

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_prompt_key_is_deterministic_and_moves_with_every_component', () => {
  const root = profileOf()
  const base = {
    session: SESSION,
    run: root.logicalRun,
    authorityRootId: root.authorityRoot,
    origin: promptOrigin('ManagerGuard'),
    agent: 'coder',
    payload: 'pd-1',
    sequence: 1,
  }

  const derive = (value) =>
    authority.derivePromptKey(
      H,
      value.session,
      value.run,
      value.authorityRootId,
      value.origin,
      value.agent,
      value.payload,
      value.sequence,
    )

  assert.equal(
    derive(base),
    `H(${['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'msg_u1', 'ManagerGuard', 'coder', 'pd-1', '1'].join('\u001f')})`,
  )
  assert.equal(derive(base), derive(base), 'same logical dispatch is deterministic')

  const variants = {
    session: { ...base, session: 'ses_b' },
    origin: { ...base, origin: promptOrigin('ReviewerGuard') },
    agent: { ...base, agent: 'reviewer' },
    payload: { ...base, payload: 'pd-2' },
    sequence: { ...base, sequence: 2 },
  }

  for (const name of ['session', 'origin', 'agent', 'payload', 'sequence']) {
    assert.notEqual(derive(variants[name]), derive(base), `${name} must participate in PromptKey`)
  }
})

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_claim_scope_names_exactly_session_run_origin_and_payload', () => {
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.logicalRun,
    promptOrigin('ManagerGuard'),
    'pd-guard',
  )

  assert.equal(scope, ['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'ManagerGuard', 'pd-guard'].join('\u001f'))

  assert.equal(
    authority.claimScopeDigest(SESSION, null, { kind: 'HostInternal', label: 'HostInternal' }, 'pd-guard'),
    ['ses_a', '\u0000absent', 'HostInternal', 'pd-guard'].join('\u001f'),
  )
})

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution', () => {
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.logicalRun,
    promptOrigin('ReviewerGuard'),
    'pd-same',
  )

  let projection = authority.registerAuthority(root, authority.empty)
  assert.equal(authority.nextClaimSequence(scope, projection), 1)

  const claimAt = (n) =>
    authority.claimContinuation(`pk_${n}`, SESSION, 'ReviewerGuard', root, 'coder', 'pd-same')

  projection = authority.registerClaim(claimAt(1), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authority.abandonClaim('pk_1', projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authority.registerClaim(claimAt(2), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 3)
})

// ── DISPATCH-PROTOCOL-007: runtime-start stamp is audit-only ──

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority', () => {
  const root = profileOf()
  const key = 'pk_r'
  const projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'ManagerGuard', root, 'coder', 'pd-r'),
    authority.registerAuthority(root, authority.empty),
  )
  const claim = findClaim(projection, key)

  assert.equal(claim.claimedAtRuntimeStartCount, 0)
  assert.deepEqual(dispatch.runtimeStartPolicy(), {
    claimStamp: 'workspace-runtime-start-count',
    advancesWorkspaceWatermark: true,
    restartRecoveryAuthority: false,
  })
})

// ── DISPATCH-PROTOCOL-010: root profile cannot express a model ──

test('WHAT[DISPATCH-PROTOCOL-010] DP_010_authority_root_profile_cannot_express_a_model', () => {
  const profile = profileOf()
  assert.deepEqual(
    { ...profile, model: profile.model },
    {
      session: 'ses_a',
      logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
      authorityRoot: 'msg_u1',
      authorityKind: 'HumanRoot',
      identitySeed: profile.identitySeed,
      participantIdentity: profile.participantIdentity,
      model: undefined,
    },
  )
})

// ── DISPATCH-PROTOCOL-002: root claim carries payload digest ──

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_claim_records_payload_digest_and_effective_agent', () => {
  const claim = authority.claimAgentOwnerRoot(
    'pk_o',
    SESSION,
    'pd-owner',
    inheritedSeed('manager', 'msg-claim-owner'),
  )
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)
  assert.deepEqual(
    {
      origin: claim.value.origin,
      payloadDigest: claim.value.payloadDigest,
      effectiveAgent: claim.value.effectiveAgent,
      receipt: claim.value.receipt,
    },
    { origin: 'AuthorityRoot', payloadDigest: 'pd-owner', effectiveAgent: 'manager', receipt: null },
  )
})

// ── DISPATCH-PROTOCOL-001: PromptDispatcher is the single writer ──

test('WHAT[DISPATCH-PROTOCOL-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime', () => {
  const surface = dispatch.sendMemberObservation()
  assert.ok(surface.members.length >= 6, `send surface must exist, got ${surface.members.length}`)
  for (const name of surface.members) {
    assert.match(name, /^Send/, `${name} must be a PromptDispatcher.Runtime member`)
  }
  for (const member of ['SendAgentOwnerRoot', 'SendContinuation', 'SendInteractionRepair', 'SendManagerIdleEncouragement']) {
    assert.ok(surface.members.includes(member), `${member} must exist on the PromptDispatcher.Runtime send surface`)
  }
  assert.equal(surface.owner, 'PromptDispatcher.Runtime')
  assert.equal(surface.standaloneFireAndForget, false, 'no standalone postPromptFireAndForget bypass may exist')
})
