// tests/unit/Prompt/authority.test.mjs — PROMPT-001/002/003/004/009, AGENT-004/005.
//
// Who may change a Logical Run's execution profile, and who may only extend it.
//
// These rebuild coverage that stopped running at `c3c35756`, when five test files
// were dropped from the `.fsproj` but left on disk. Two of the original assertions
// are NOT rebuilt: they asserted that an `accepted-*` transport receipt could carry
// authority, which PROMPT-005 now forbids outright. The replacement for those two is
// `PROMPT_001_a_transport_receipt_can_never_become_an_authority_root` below — the
// same situation, asserted the other way round.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  authority,
  authorityRun,
  caseOf,
  continuationKind,
  idValue,
  isAdmissionShaped,
  isSome,
  mapCount,
  mapTryFind,
  physicalUser,
  promoteToAuthorityRoot,
  promptKey,
  promptOrigin,
  providerRun,
  rootKind,
  runtimeId,
  sessionId,
  transportReceipt,
} from '../support/domain.mjs'

// A visible stand-in for sha256. Real hashing would make every expectation an
// opaque hex blob, and the property under test is which fields enter the digest —
// not the digest function.
const H = (input) => `H(${input})`

const RUNTIME = runtimeId('rt_1')
const SESSION = sessionId('ses_a')
const PHYSICAL = physicalUser('msg_u1')

const rootFor = (agent = 'fast-coder', physical = PHYSICAL, kind = rootKind.human) =>
  authorityRun.createAuthorityRoot(H, RUNTIME, SESSION, kind, physical, agent)

const profileOf = (...args) => {
  const built = rootFor(...args)
  assert.equal(built.ok, true, built.ok ? '' : `createAuthorityRoot rejected: ${built.error}`)
  return built.value
}

/** The whole profile as plain comparable text, so a renamed field cannot pass. */
const readProfile = (profile) => ({
  session: idValue.session(profile.SessionId),
  logicalRun: idValue.logicalRun(profile.LogicalRunId),
  authorityRoot: idValue.authorityRoot(profile.AuthorityRootUserMessageId),
  authorityKind: caseOf(profile.AuthorityKind),
  selectedAgent: profile.SelectedAgent,
  peerAgent: profile.PeerAgent,
  canonicalRole: authority.roleLabel(profile.CanonicalRole),
  selectedTier: authority.tierLabel(profile.SelectedTier),
})

// ── PROMPT-001: a physical user message is not an authority turn ─────────────

test('PROMPT_001_authority_root_id_is_reachable_only_by_promoting_a_physical_message', () => {
  // The clause says the two are different kinds. The type system is where that is
  // enforced: `promoteToAuthorityRoot` is the single crossing, and PROMPT-005 lets
  // it happen only once `PhysicalAccepted` is established.
  assert.equal(idValue.authorityRoot(promoteToAuthorityRoot(PHYSICAL)), 'msg_u1')

  // Same text, but the ids are different concepts, so the profile records the
  // promoted one and nothing else.
  assert.equal(readProfile(profileOf()).authorityRoot, 'msg_u1')
})

test('PROMPT_001_a_transport_receipt_can_never_become_an_authority_root', () => {
  // `accepted-*` is what the Host returns from a fire-and-forget send. The deleted
  // tests treated it as a message id and let it carry authority; PROMPT-005 calls
  // it a receipt, and only a real `chat.message` produces PhysicalAccepted.
  assert.equal(isAdmissionShaped(transportReceipt('accepted-1a2b')), true)
  assert.equal(isAdmissionShaped(transportReceipt('msg_real')), false)

  // There is no function from TransportReceipt to AuthorityRootUserMessageId. The
  // facade exposes every crossing the production code has, so its absence here is
  // the assertion.
  assert.equal(typeof promoteToAuthorityRoot, 'function')
  assert.equal(Object.keys({ ...authorityRun }).includes('promoteReceipt'), false)
})

// ── PROMPT-002: only an Authority Root fixes the execution profile ───────────

test('PROMPT_002_authority_root_profile_cannot_express_a_model', () => {
  // "Model = None always" is not a runtime check here — the profile has no field
  // for it, so an overriding model is unrepresentable. A new field named for a
  // model would fail this immediately.
  assert.deepEqual(Object.keys(profileOf()), [
    'SessionId',
    'LogicalRunId',
    'AuthorityRootUserMessageId',
    'AuthorityKind',
    'SelectedAgent',
    'PeerAgent',
    'CanonicalRole',
    'SelectedTier',
  ])
})

test('PROMPT_002_root_derives_peer_role_and_tier_from_the_selected_agent_alone', () => {
  assert.deepEqual(readProfile(profileOf('fast-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'fast-coder',
    peerAgent: 'deep-coder',
    canonicalRole: 'coder',
    selectedTier: 'Fast',
  })

  // The pair is symmetric: picking the deep side makes fast the peer, and the role
  // is unchanged. Role must not follow tier (AGENT-010).
  assert.deepEqual(readProfile(profileOf('deep-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'deep-coder',
    peerAgent: 'fast-coder',
    canonicalRole: 'coder',
    selectedTier: 'Deep',
  })
})

test('AGENT_004_005_bare_legacy_agent_names_are_refused', () => {
  // A bare role name has no tier, so it cannot determine a peer, and admitting it
  // would leave the fallback pair undefined. Fail closed with a named rejection
  // rather than defaulting a tier.
  for (const bare of ['coder', 'manager', 'reviewer', 'orchestrator']) {
    const built = rootFor(bare)
    assert.equal(built.ok, false, `'${bare}' must not produce a profile`)
    assert.equal(
      built.error,
      `Legacy agent name '${bare}' is not supported. Managed agents require explicit fast-/deep- names.`,
    )
  }

  // Typed rejections, so a caller can branch on the reason without parsing prose.
  // Three distinct reasons: a known role without a tier, an unknown managed agent,
  // and text that is not an agent name at all.
  assert.equal(caseOf(authority.parseAgentName('coder').error), 'LegacyAgentName')
  assert.equal(caseOf(authority.parseAgentName('nonsense').error), 'Malformed')
})

test('PROMPT_002_a_new_root_replaces_the_profile_and_clears_everything_run_scoped', () => {
  const first = profileOf('fast-coder')
  let projection = authorityRun.registerAuthority(first, authority.empty)

  // Populate every run-scoped map so the reset is observable rather than vacuous.
  const key = promptKey('pk_1')
  const claim = authorityRun.claimContinuation(
    key,
    SESSION,
    continuationKind.of('ManagerGuard'),
    first,
    'fast-coder',
    'pd-guard',
  )
  projection = authorityRun.registerClaim(claim, projection)
  projection = authorityRun.acceptClaim(key, physicalUser('msg_c1'), projection)

  assert.deepEqual(
    {
      sequences: mapCount(projection.ClaimSequences),
      acceptedContinuations: mapCount(projection.AcceptedContinuationIds),
    },
    { sequences: 1, acceptedContinuations: 1 },
  )

  const second = profileOf('deep-reviewer', physicalUser('msg_u2'))
  const after = authorityRun.registerAuthority(second, projection)

  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(second))
  assert.deepEqual(readProfile(after.LastAuthorityProfile), readProfile(second))
  assert.deepEqual(
    {
      pending: mapCount(after.PendingClaims),
      sequences: mapCount(after.ClaimSequences),
      acceptedContinuations: mapCount(after.AcceptedContinuationIds),
    },
    { pending: 0, sequences: 0, acceptedContinuations: 0 },
    'PROMPT-002: a new root resets continuations, repair budget and claim sequences',
  )
})

// ── PROMPT-003: a continuation extends, never redefines ──────────────────────

test('PROMPT_003_a_continuation_never_replaces_the_authority_root', () => {
  const root = profileOf('fast-coder')
  const before = authorityRun.registerAuthority(root, authority.empty)

  // A continuation is dispatched on the OTHER side of the pair — that is what
  // fallback does — and must still not touch the profile (FALLBACK-004).
  const claim = authorityRun.claimContinuation(
    promptKey('pk_c'),
    SESSION,
    continuationKind.of('ProviderRetryAttempt'),
    root,
    'deep-coder',
    'pd-retry',
  )

  assert.deepEqual(
    {
      origin: caseOf(claim.Origin),
      logicalRun: idValue.logicalRun(claim.LogicalRunId),
      authorityRoot: idValue.authorityRoot(claim.AuthorityRootUserMessageId),
      effectiveAgent: claim.EffectiveAgent,
    },
    {
      origin: 'Continuation',
      logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
      authorityRoot: 'msg_u1',
      effectiveAgent: 'deep-coder',
    },
    'a continuation inherits run and root, and carries only the cursor-selected agent',
  )

  const after = authorityRun.registerClaim(claim, before)
  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(root))
  assert.deepEqual(readProfile(after.LastAuthorityProfile), readProfile(root))
})

test('PROMPT_003_every_continuation_kind_is_representable_and_none_is_a_root', () => {
  // The clause enumerates six. A kind missing from the parser would silently make
  // that prompt UnknownOrigin and fail closed at dispatch, which reads as "the
  // feature is broken" rather than "the name is unknown".
  const kinds = [
    'InteractionRepair',
    'ManagerGuard',
    'ReviewerGuard',
    'ReviewConfirmation',
    'BusyAgentNudge',
    'ProviderRetryAttempt',
  ]

  for (const name of kinds) {
    const origin = promptOrigin.continuation(continuationKind.of(name))
    assert.equal(caseOf(origin), 'Continuation')
    assert.equal(authority.originLabel(origin), name)
  }

  assert.equal(isSome(authority.tryParseContinuationKind('AuthorityRoot')), false)
  assert.throws(() => continuationKind.of('HumanRoot'), /unknown ContinuationKind/)
})

// ── PROMPT-005 / PROMPT-011: claim lifecycle and its idempotency anchor ──────

test('PROMPT_005_submit_records_the_receipt_without_resolving_the_claim', () => {
  const root = profileOf()
  const key = promptKey('pk_s')
  const claim = authorityRun.claimContinuation(
    key,
    SESSION,
    continuationKind.of('ManagerGuard'),
    root,
    'fast-coder',
    'pd-1',
  )

  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(claim, projection)
  assert.equal(isSome(mapTryFind(key, projection.PendingClaims).Receipt), false)

  const submitted = authorityRun.submitClaim(key, transportReceipt('accepted-9f'), projection)
  const stored = mapTryFind(key, submitted.PendingClaims)

  assert.deepEqual(
    {
      pending: mapCount(submitted.PendingClaims),
      receipt: idValue.transportReceipt(stored.Receipt),
    },
    { pending: 1, receipt: 'accepted-9f' },
    'Submitted keeps the claim pending: only a real chat.message resolves it',
  )
})

test('PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone', () => {
  const root = profileOf()
  const key = promptKey('pk_x')
  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('BusyAgentNudge'), root, 'fast-coder', 'pd-n'),
    projection,
  )

  const after = authorityRun.abandonClaim(key, projection)

  assert.equal(mapCount(after.PendingClaims), 0)
  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(root))
  // The sequence stays consumed. Reusing it would let the abandoned dispatch and
  // its retry derive one PromptKey for two logical acts.
  assert.equal(mapCount(after.ClaimSequences), 1)
})

test('PROMPT_011_stable_logical_run_id_is_a_function_of_runtime_session_and_root', () => {
  const id = (rt, ses, root) =>
    idValue.logicalRun(authority.stableLogicalRunId(H, runtimeId(rt), sessionId(ses), promoteToAuthorityRoot(physicalUser(root))))

  assert.equal(id('rt_1', 'ses_a', 'msg_u1'), 'H(rt_1\nses_a\nmsg_u1)')
  assert.equal(id('rt_1', 'ses_a', 'msg_u1'), id('rt_1', 'ses_a', 'msg_u1'))

  // Each input must move the id, or two distinct runs would share one.
  const base = id('rt_1', 'ses_a', 'msg_u1')
  assert.notEqual(id('rt_2', 'ses_a', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_b', 'msg_u1'), base)
  assert.notEqual(id('rt_1', 'ses_a', 'msg_u2'), base)
})

test('PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload', () => {
  // The scope is a joined string, not a hash, so the four components are readable.
  // PROMPT-011 names these four; a fifth would change which dispatches count as
  // "the same logical act repeated".
  const scope = authority.claimScopeDigest(
    SESSION,
    profileOf().LogicalRunId,
    promptOrigin.continuation(continuationKind.of('ManagerGuard')),
    'pd-guard',
  )

  assert.equal(scope, ['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'ManagerGuard', 'pd-guard'].join('\u001f'))

  // An absent run gets an explicit marker rather than an empty segment, so
  // "no run yet" cannot collide with "a run whose id is blank".
  assert.equal(
    authority.claimScopeDigest(SESSION, undefined, promptOrigin.hostInternal, 'pd-guard'),
    ['ses_a', '\u0000absent', 'HostInternal', 'pd-guard'].join('\u001f'),
  )
})

test('PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution', () => {
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.LogicalRunId,
    promptOrigin.continuation(continuationKind.of('ReviewerGuard')),
    'pd-same',
  )

  let projection = authorityRun.registerAuthority(root, authority.empty)
  assert.equal(authority.nextClaimSequence(scope, projection), 1)

  const claimAt = (n) =>
    authorityRun.claimContinuation(
      promptKey(`pk_${n}`),
      SESSION,
      continuationKind.of('ReviewerGuard'),
      root,
      'fast-coder',
      'pd-same',
    )

  projection = authorityRun.registerClaim(claimAt(1), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  // Abandon the first, then claim the same payload again. The sequence must not
  // roll back, or both dispatches derive the same PromptKey.
  projection = authorityRun.abandonClaim(promptKey('pk_1'), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authorityRun.registerClaim(claimAt(2), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 3)
})

test('PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component', () => {
  const root = profileOf()
  const base = {
    session: SESSION,
    run: root.LogicalRunId,
    authorityRootId: root.AuthorityRootUserMessageId,
    origin: promptOrigin.continuation(continuationKind.of('ManagerGuard')),
    agent: 'fast-coder',
    payload: 'pd-1',
    sequence: 1,
  }

  const derive = (o) =>
    idValue.promptKey(
      authority.derivePromptKey(H, o.session, o.run, o.authorityRootId, o.origin, o.agent, o.payload, o.sequence),
    )

  assert.equal(
    derive(base),
    `H(${['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'msg_u1', 'ManagerGuard', 'fast-coder', 'pd-1', '1'].join('\u001f')})`,
  )
  assert.equal(derive(base), derive(base), 'the same logical dispatch must derive the same key on any process')

  const variants = {
    session: { ...base, session: sessionId('ses_b') },
    origin: { ...base, origin: promptOrigin.continuation(continuationKind.of('ReviewerGuard')) },
    agent: { ...base, agent: 'deep-coder' },
    payload: { ...base, payload: 'pd-2' },
    sequence: { ...base, sequence: 2 },
  }

  for (const [name, variant] of Object.entries(variants)) {
    assert.notEqual(derive(variant), derive(base), `${name} must participate in the PromptKey`)
  }
})

test('PROMPT_011_recovery_budget_is_folded_from_plugin_starts_not_written', () => {
  const root = profileOf()
  const key = promptKey('pk_r')
  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('ManagerGuard'), root, 'fast-coder', 'pd-r'),
    projection,
  )

  assert.equal(authority.recoveryAttemptBudget, 3)
  const spentAfter = []

  for (let start = 1; start <= 4; start += 1) {
    projection = authority.countRecoveryAttempt(projection)
    const claim = mapTryFind(key, projection.PendingClaims)
    spentAfter.push({ starts: start, attempts: claim.RecoveryAttempts, spent: authority.recoveryBudgetSpent(claim) })
  }

  assert.deepEqual(spentAfter, [
    { starts: 1, attempts: 1, spent: false },
    { starts: 2, attempts: 2, spent: false },
    { starts: 3, attempts: 3, spent: true },
    { starts: 4, attempts: 4, spent: true },
  ])
})

test('FALLBACK_008_one_terminal_provider_run_earns_exactly_one_repair', () => {
  const root = profileOf()
  const terminal = providerRun('run_term')
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const alreadyClaimed = () =>
    authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, terminal, 'empty', projection)

  assert.equal(alreadyClaimed(), false)

  const repair = authorityRun.claimContinuation(
    promptKey('pk_rep'),
    SESSION,
    continuationKind.of('InteractionRepair'),
    root,
    'fast-coder',
    authority.repairPayloadDigest(terminal, 'empty'),
  )
  projection = authorityRun.registerClaim(repair, projection)

  assert.equal(alreadyClaimed(), true, 'the budget is derived from ClaimSequences, so it survives a restart')

  // A different terminal is a different occasion, and a different repair kind on
  // the same terminal is too — the digest names both.
  assert.equal(
    authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, providerRun('run_other'), 'empty', projection),
    false,
  )
  assert.equal(authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, terminal, 'xml_only', projection), false)

  // Abandoning the repair must not license a second one.
  projection = authorityRun.abandonClaim(promptKey('pk_rep'), projection)
  assert.equal(alreadyClaimed(), true)
})

// ── PROMPT-004 / PROMPT-009: origin resolution order, failing closed ─────────

test('PROMPT_009_resolution_order_is_accepted_then_claimed_then_compaction_then_root', () => {
  const root = profileOf('fast-coder', PHYSICAL, rootKind.agentOwner)
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const claimedKey = promptKey('pk_claimed')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(
      claimedKey,
      SESSION,
      continuationKind.of('ReviewConfirmation'),
      root,
      'fast-coder',
      'pd-c',
    ),
    projection,
  )

  const acceptedKey = promptKey('pk_accepted')
  const acceptedPhysical = physicalUser('msg_accepted')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(
      acceptedKey,
      SESSION,
      continuationKind.of('BusyAgentNudge'),
      root,
      'fast-coder',
      'pd-a',
    ),
    projection,
  )
  projection = authorityRun.acceptClaim(acceptedKey, acceptedPhysical, projection)

  const unseen = physicalUser('msg_unseen')

  assert.deepEqual(
    {
      accepted: authorityRun.resolveKnownOrigin(acceptedPhysical, undefined, false, projection),
      claimed: authorityRun.resolveKnownOrigin(unseen, claimedKey, false, projection),
      compaction: authorityRun.resolveKnownOrigin(unseen, undefined, true, projection),
      registeredRoot: authorityRun.resolveKnownOrigin(unseen, promptKey('pk_unknown'), false, projection),
      nothing: authorityRun.resolveKnownOrigin(unseen, undefined, false, projection),
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

test('PROMPT_004_009_an_accepted_id_outranks_host_compaction', () => {
  // Order matters, not just membership: a message the plugin itself dispatched and
  // saw accepted must stay a continuation even on a turn where the Host also
  // reports compaction. Reading compaction first would relabel real work
  // HostInternal and drop it out of the Logical Run.
  const root = profileOf()
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const key = promptKey('pk_both')
  const physical = physicalUser('msg_both')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('ManagerGuard'), root, 'fast-coder', 'pd-b'),
    projection,
  )
  projection = authorityRun.acceptClaim(key, physical, projection)

  assert.equal(authorityRun.resolveKnownOrigin(physical, undefined, true, projection), 'Continuation')
})

test('PROMPT_004_a_human_root_is_never_inferred_by_a_pure_function', () => {
  // HumanRoot requires proven external acceptance carrying an explicit agent.
  // `resolveKnownOrigin` cannot observe that, so it must never return HumanRoot —
  // an unproven message falls to UnknownOrigin and the dispatcher fails closed.
  const humanRoot = profileOf('fast-coder', PHYSICAL, rootKind.human)
  const projection = authorityRun.registerAuthority(humanRoot, authority.empty)

  assert.equal(caseOf(projection.ActiveLogicalRun.AuthorityKind), 'HumanRoot')
  assert.equal(
    authorityRun.resolveKnownOrigin(physicalUser('msg_new'), promptKey('pk_any'), false, projection),
    'UnknownOrigin',
    'an active HumanRoot must not make later unknown messages look like roots',
  )
})

test('PROMPT_004_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active', () => {
  // Structural lock on PromptIngress.resolveOrigin: mid-run, ExplicitAgent alone
  // must not open a new HumanRoot (plugin continuation without PromptKey would
  // reset the fallback cursor). First external prompt (no ActiveLogicalRun) may
  // still become HumanRoot when the agent name is valid.
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const ingress = readFileSync(join(root, 'src/Wanxiangshu/Application/Prompting/PromptIngress.fs'), 'utf8')

  assert.match(
    ingress,
    /ActiveProfile sessionId/,
    'HumanRoot promotion must gate on ActiveLogicalRun absence',
  )
  assert.match(
    ingress,
    /Some agent, None when isValidAgent agent/,
    'HumanRoot only when ExplicitAgent valid AND no active run',
  )
  // Fail-closed arm: mid-run / missing agent stays UnknownOrigin.
  assert.match(
    ingress,
    /\| _ -> PromptAuthority\.PromptOrigin\.UnknownOrigin/,
    'non-first-prompt UnknownOrigin must stay UnknownOrigin',
  )
  // Forbid the old fail-open: UnknownOrigin + valid agent alone → HumanRoot
  // without consulting ActiveProfile.
  assert.doesNotMatch(
    ingress,
    /match message\.ExplicitAgent with[\s\S]{0,120}Some agent when isValidAgent agent[\s\S]{0,80}HumanRoot/,
    'must not promote on ExplicitAgent alone without ActiveProfile gate',
  )
  assert.match(
    ingress,
    /ExplicitAgent, runtime\.ActiveProfile/,
    'promotion pairs ExplicitAgent with ActiveProfile (None = first prompt only)',
  )
})

test('PROMPT_009_accepting_an_authority_root_claim_does_not_enter_the_continuation_map', () => {
  // The map answers "was this message a continuation, and of what kind". Recording
  // a root there would let a later lookup call it a continuation, and REVIEW-003
  // forbids reusing it as review evidence in either direction.
  const key = promptKey('pk_owner')
  const claim = authorityRun.claimAgentOwnerRoot(key, SESSION, 'pd-owner', 'fast-manager')
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)

  assert.deepEqual(
    {
      origin: caseOf(claim.value.Origin),
      label: authority.originLabel(claim.value.Origin),
      // No run yet: the id derives from a physical message that does not exist
      // until the Host accepts.
      hasRun: isSome(claim.value.LogicalRunId),
      hasRoot: isSome(claim.value.AuthorityRootUserMessageId),
      effectiveAgent: claim.value.EffectiveAgent,
    },
    { origin: 'AuthorityRoot', label: 'AgentOwnerRoot', hasRun: false, hasRoot: false, effectiveAgent: 'fast-manager' },
  )

  let projection = authorityRun.registerClaim(claim.value, authority.empty)
  const physical = physicalUser('msg_owner')
  projection = authorityRun.acceptClaim(key, physical, projection)

  assert.deepEqual(
    {
      pending: mapCount(projection.PendingClaims),
      acceptedContinuations: mapCount(projection.AcceptedContinuationIds),
    },
    { pending: 0, acceptedContinuations: 0 },
  )
  assert.equal(authorityRun.resolveKnownOrigin(physical, undefined, false, projection), 'UnknownOrigin')
})

test('PROMPT_002_agent_owner_root_claims_reject_bare_legacy_names_too', () => {
  // Same parser, same refusal. A second entry point admitting a bare name would
  // reintroduce the undefined fallback pair one layer up.
  const claim = authorityRun.claimAgentOwnerRoot(promptKey('pk_b'), SESSION, 'pd', 'manager')
  assert.equal(claim.ok, false)
  assert.equal(
    claim.error,
    "Legacy agent name 'manager' is not supported. Managed agents require explicit fast-/deep- names.",
  )
})
