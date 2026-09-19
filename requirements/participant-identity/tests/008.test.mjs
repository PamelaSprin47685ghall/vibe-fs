import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const H = (value) => `H(${value})`
const canonicalIdentityOf = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    participant: resolved.identity.name,
    role: resolved.identity.role,
    persona: resolved.identity.persona,
    personaCatalogVersion: resolved.identity.catalogVersion,
    origin: resolved.identity.origin,
  }
}
const rootSelection = (agent) => ({
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: canonicalIdentityOf(agent),
})
const rootProfile = (
  session = 'ses_owner',
  physical = 'msg_owner_root',
  agent = 'manager',
) => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-identity-lineage',
    session,
    'HumanRoot',
    physical,
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const inheritedSeed = (child, owner) => {
  const result = authority.issueInheritedIdentitySeed(child, owner)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[participant-identity-008] inherited identity records the exact durable owner witness', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('engineer', owner)

  assert.deepEqual(
    {
      kind: seed.kind,
      ownerSession: seed.ownerSession,
      ownerLogicalRun: seed.ownerLogicalRun,
      ownerAuthorityRoot: seed.ownerAuthorityRoot,
    },
    {
      kind: 'InheritedFromOwner',
      ownerSession: owner.session,
      ownerLogicalRun: owner.logicalRun,
      ownerAuthorityRoot: owner.authorityRoot,
    },
  )
  assert.deepEqual(
    {
      participant: seed.participantIdentity.participant,
      role: seed.participantIdentity.role,
      persona: seed.participantIdentity.persona,
      personaCatalogVersion: seed.participantIdentity.personaCatalogVersion,
      origin: seed.participantIdentity.origin,
    },
    {
      participant: 'engineer',
      role: 'engineer',
      persona: owner.participantIdentity.persona,
      personaCatalogVersion: owner.participantIdentity.personaCatalogVersion,
      origin: 'InheritedFromOwner',
    },
  )
})
test('WHAT[participant-identity-008] rejects stale owner identity evidence', () => {
  const seed = inheritedSeed('devops', rootProfile())
  const currentOwnerRun = rootProfile('ses_owner', 'msg_fresh_owner_root')

  const validation = authority.validateInheritedIdentitySeed(currentOwnerRun, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerLogicalRunIdMismatch',
    expected: currentOwnerRun.logicalRun,
    actual: seed.ownerLogicalRun,
  })
})
test('WHAT[participant-identity-008] closed owner run rejects its inherited identity evidence', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)

  const validation = authority.validateInheritedIdentitySeedAgainstActiveOwner(null, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerAuthorityNotActive',
    expected: owner.session,
    actual: '',
  })
})
test('WHAT[participant-identity-008] derived identity rejects root-selection evidence', () => {
  const owner = rootProfile()

  const validation = authority.validateInheritedIdentitySeed(owner, owner.identitySeed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'ExpectedInheritedFromOwner',
    expected: 'InheritedFromOwner',
    actual: 'RootSelection',
  })
})
test('WHAT[participant-identity-008] inherited identity rejects a different owner session', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const wrongOwner = { ...owner, session: 'ses_different_owner' }

  const validation = authority.validateInheritedIdentitySeed(wrongOwner, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerSessionIdMismatch',
    expected: wrongOwner.session,
    actual: owner.session,
  })
})
test('WHAT[participant-identity-008] inherited identity rejects a different authority root', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const wrongRoot = { ...owner, authorityRoot: 'msg_different_root' }

  const validation = authority.validateInheritedIdentitySeed(wrongRoot, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerAuthorityRootUserMessageIdMismatch',
    expected: wrongRoot.authorityRoot,
    actual: owner.authorityRoot,
  })
})
test('WHAT[participant-identity-008] durable inherited seed round-trips without re-resolution', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const claimed = authority.claimAgentOwnerRoot('pk_child', 'ses_child', 'digest-child', seed)
  assert.equal(claimed.ok, true, claimed.ok ? '' : claimed.error)

  const replayedClaim = JSON.parse(JSON.stringify(claimed.value))
  assert.deepEqual(authority.projectClaimIdentitySeed(replayedClaim), seed)

  const serialized = authority.serializeIdentitySeed(
    authority.projectClaimIdentitySeed(replayedClaim),
  )
  assert.equal(serialized.ok, true, serialized.ok ? '' : serialized.error)
  const replayed = authority.rehydrateIdentitySeed(serialized.value)

  assert.equal(replayed.ok, true, replayed.ok ? '' : replayed.error)
  assert.deepEqual(replayed.value, seed)
  assert.deepEqual(authority.validateInheritedIdentitySeed(owner, replayed.value), {
    ok: true,
    value: seed.participantIdentity,
    error: null,
  })
})
test('WHAT[participant-identity-008] raw legacy PeerAgent fields are ignored and never re-encoded', () => {
  const canonical = canonicalIdentityOf('engineer')
  const legacySeed = {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { ...canonical, peerAgent: 'engineer', PeerAgent: 'engineer' },
  }
  const created = authority.createAuthorityRoot(
    H,
    'runtime-identity-lineage',
    'ses_legacy_drop',
    'HumanRoot',
    'msg_legacy_drop',
    legacySeed,
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  assert.deepEqual(created.value.participantIdentity, {
    participant: canonical.participant,
    role: canonical.role,
    persona: canonical.persona,
    personaCatalogVersion: canonical.personaCatalogVersion,
    origin: canonical.origin,
  })
  assert.equal(JSON.stringify(created.value).includes('eerAgent'), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const journalCodec = await import("../../../dist/Persistence/Journal/CodecSurface.js");
const factCodec = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");

const H = (value) => `H(${value})`
const rootSeed = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'engineer',
    role: 'engineer',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const createRoot = (
  kind = 'HumanRoot',
  seed = rootSeed,
  session = 'ses-recovery',
  physical = 'msg-recovery',
) => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-identity-recovery',
    session,
    kind,
    physical,
    seed,
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  return created.value
}
const register = (profile) => {
  const projection = authority.registerAuthority(profile, authority.empty)
  assert.equal(projection.activeLogicalRun?.logicalRun, profile.logicalRun)
  return projection
}
const authorityFact = (profile, identitySeed = profile.identitySeed) => ({
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: identitySeed,
  },
})
const envelope = (fact) => ({
  runtime: 'runtime-identity-recovery',
  seq: 1,
  observedAt: '2026-08-30T00:00:00Z',
  id: 'identity-recovery-event',
  stream: { kind: 'Session', id: fact.payload.SessionId },
  providerRun: null,
  fact,
})
const journalRoundTripPayload = (fact) => {
  const factDecoded = factCodec.decode(factCodec.encode(fact))
  assert.equal(factDecoded.ok, true, factDecoded.ok ? '' : factDecoded.error)

  const line = journalCodec.serialize(envelope({ ...fact, payload: factDecoded.payload }))
  const decoded = journalCodec.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  return decoded.value.fact.payload
}
const profileFromPayload = (payload) => ({
  session: payload.SessionId,
  logicalRun: payload.LogicalRunId,
  authorityRoot: payload.AuthorityRootUserMessageId,
  authorityKind: payload.AuthorityKind,
  identitySeed: payload.IdentitySeed,
  participantIdentity: payload.IdentitySeed.participantIdentity,
})
const authorityCase = (value) => {
  if (Array.isArray(value)) {
    if (value[0] === 'AuthorityRootAccepted') return value
    for (const item of value) {
      const found = authorityCase(item)
      if (found) return found
    }
  } else if (value && typeof value === 'object') {
    for (const item of Object.values(value)) {
      const found = authorityCase(item)
      if (found) return found
    }
  }
  return null
}
const legacyHumanRootLine = (profile) => {
  const current = JSON.parse(factCodec.encode(authorityFact(profile)))
  const tagged = authorityCase(current)
  assert.notEqual(tagged, null)
  const payload = tagged[1]
  delete payload.SchemaVersion
  delete payload.IdentitySeed
  payload.SelectedAgent = 'engineer'
  payload.PeerAgent = 'engineer'
  payload.CanonicalRole = 'engineer'
  payload.SelectedTier = 'deep'
  return JSON.stringify(current)
}
const inheritedProfile = () => {
  const owner = createRoot()
  const issued = authority.issueInheritedIdentitySeed('devops', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return createRoot('AgentOwnerRoot', issued.value, 'ses-recovery-child', 'msg-recovery-child')
}

test('WHAT[participant-identity-008] current v2 durable identity recovers exact participant and owner provenance', () => {
  const profile = inheritedProfile()
  const payload = journalRoundTripPayload(authorityFact(profile))
  assert.deepEqual(payload, {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: profile.identitySeed,
  })

  assert.deepEqual(authority.recoverActiveIdentity(register(profileFromPayload(payload))), {
    ok: true,
    value: {
      participantIdentity: profile.participantIdentity,
      identitySeed: profile.identitySeed,
    },
    error: '',
  })
})
test('WHAT[participant-identity-008] supported legacy HumanRoot deterministically recovers its upgraded identity', () => {
  const profile = createRoot()
  const legacy = legacyHumanRootLine(profile)
  assert.match(legacy, /PeerAgent/)
  const first = factCodec.decode(legacy)
  const second = factCodec.decode(legacy)
  assert.equal(first.ok, true, first.ok ? '' : first.error)
  assert.equal(second.ok, true, second.ok ? '' : second.error)
  assert.deepEqual(second.payload, first.payload)
  assert.deepEqual(first.payload, {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: rootSeed,
  })
  assert.equal(JSON.stringify(first.payload).includes('PeerAgent'), false)

  assert.deepEqual(authority.recoverActiveIdentity(register(profileFromPayload(first.payload))), {
    ok: true,
    value: {
      participantIdentity: rootSeed.participantIdentity,
      identitySeed: rootSeed,
    },
    error: '',
  })
})
test('WHAT[participant-identity-008] missing active authority rejects even when LastAuthorityProfile is present', () => {
  const projection = register(createRoot())
  const historicalOnly = { ...projection, activeLogicalRun: null }

  assert.deepEqual(authority.recoverActiveIdentity(historicalOnly), {
    ok: false,
    value: null,
    error: 'MissingActiveAuthority',
  })
})
test('WHAT[participant-identity-008] rejects corrupt identity provenance', () => {
  const rootProjection = register(createRoot())
  const inheritedProjection = register(inheritedProfile())
  const corruptions = [
    [rootProjection, (active) => { active.identitySeed.participantIdentity.persona = 'WrongPersona' }],
    [rootProjection, (active) => { active.identitySeed.participantIdentity.personaCatalogVersion = 99 }],
    [inheritedProjection, (active) => { active.identitySeed.participantIdentity.origin = 'ResolvedAtRoot' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerSession = '' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerLogicalRun = '' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerAuthorityRoot = '' }],
  ]

  for (const [projection, corrupt] of corruptions) {
    const candidate = structuredClone(projection)
    corrupt(candidate.activeLogicalRun)
    const recovered = authority.recoverActiveIdentity(candidate)
    assert.equal(recovered.ok, false)
    assert.equal(recovered.value, null)
    assert.notEqual(recovered.error, '')
  }
})
test('WHAT[participant-identity-008] closed exact run cannot be recovered as current', () => {
  const profile = createRoot()
  const closed = authority.closeAuthority(
    profile.logicalRun,
    profile.authorityRoot,
    register(profile),
  )
  assert.equal(closed.ok, true, closed.ok ? '' : closed.error)
  assert.notEqual(closed.value.lastAuthorityProfile, null)
  assert.equal(closed.value.activeLogicalRun, null)

  assert.deepEqual(authority.recoverActiveIdentity(closed.value), {
    ok: false,
    value: null,
    error: 'MissingActiveAuthority',
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Attempt = await import("../../../dist/Context/Companion/CompressionSurface.js");
const ProviderFailure = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");
const Dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const Fission = await import("../../../dist/Execution/Fission/Surface.js");
const Authority = await import("../../../dist/Interaction/Authority/Surface.js");
const Runtime = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const Strength = await import("../../../dist/Strength/Surface.js");
const Persona = await import("../../../dist/Participant/Persona/Surface.js");

const hash = (value) => `H(${value})`
const canonicalIdentityOf = (agent) => {
  const resolved = Persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    participant: resolved.identity.name,
    role: resolved.identity.role,
    persona: resolved.identity.persona,
    personaCatalogVersion: resolved.identity.catalogVersion,
    origin: resolved.identity.origin,
  }
}
const rootSelection = (agent) => ({
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: canonicalIdentityOf(agent),
})
const rootProfile = (agent, session = `ses_${agent}`) => {
  const result = Runtime.createAuthorityRoot(
    hash,
    'runtime-participant-identity-consumers',
    session,
    'HumanRoot',
    `msg_${agent}`,
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const attemptPlan = (role, kind = 'work-main') =>
  Attempt.attemptPlan({ role, kind, noCandidateReason: 'NoCoverage' })
const inheritedSeed = (childAgent, owner) => {
  const issued = Runtime.issueInheritedIdentitySeed(childAgent, owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return issued.value
}
const personaVersion = (identity) => ({
  persona: identity.persona,
  personaCatalogVersion: identity.personaCatalogVersion,
})
const assertCanonicalIdentity = (actual, expected, label) => {
  assert.equal(actual.participant, expected.participant, `${label} participant`)
  assert.equal(actual.role, expected.role, `${label} role`)
  assert.equal(actual.persona, expected.persona, `${label} persona`)
  assert.equal(actual.personaCatalogVersion, expected.personaCatalogVersion, `${label} version`)
  assert.equal(actual.origin, expected.origin, `${label} origin`)
}
const assertNoLegacyIdentityFields = (value, label) => {
  const text = JSON.stringify(value)
  for (const token of ['PeerAgent', 'peerAgent', 'eerAgent', 'EffectiveAgent', 'effectiveAgent', 'ffectiveAgent', 'cursor', 'Cursor']) {
    assert.equal(text.includes(token), false, `${label} must not contain ${token}: ${text}`)
  }
}

test('WHAT[participant-identity-008] child identity inherits the parent Persona and version across roles', () => {
  const parent = rootProfile('engineer', 'ses_identity_parent')
  const child = inheritedSeed('manager', parent)

  assert.equal(child.participantIdentity.participant, 'manager')
  assert.equal(child.participantIdentity.role, 'manager')
  assert.deepEqual(personaVersion(child.participantIdentity), personaVersion(parent.participantIdentity))
  assert.equal(child.ownerSession, parent.session)
  assert.equal(child.ownerLogicalRun, parent.logicalRun)
  assert.equal(child.ownerAuthorityRoot, parent.authorityRoot)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertJsData } = await import("../../verification-system/tests/support/js-contract.mjs");

const identity = await import('../../../dist/Participant/Persona/Surface.js')
const EXPECTED = {
  orchestrator: { role: 'orchestrator', persona: 'Director' },
  manager: { role: 'manager', persona: 'Lead' },
  engineer: { role: 'engineer', persona: 'Engineer' },
  devops: { role: 'devops', persona: 'Operator' },
  blogger: { role: 'blogger', persona: 'Chronicler' },
  bookkeeper: { role: 'bookkeeper', persona: 'Curator' },
  predictor: { role: 'engineer', persona: 'Engineer' },
}
const expectedView = (name, origin = 'ResolvedAtRoot') => ({
  name,
  role: EXPECTED[name].role,
  persona: EXPECTED[name].persona,
  catalogVersion: 1,
  origin,
})
const assertCanonicalIdentity = (actual, expected, label) => {
  assert.equal(actual.name, expected.name, `${label} participant`)
  assert.equal(actual.role, expected.role, `${label} role`)
  assert.equal(actual.persona, expected.persona, `${label} persona`)
  assert.equal(actual.catalogVersion, expected.catalogVersion, `${label} version`)
  assert.equal(actual.origin, expected.origin, `${label} origin`)
}
const rehydrate = (view, ownerName = '') =>
  identity.rehydrateParticipantIdentity(
    ownerName,
    view.name,
    view.role,
    'deep',
    'retired-peer-slot',
    view.persona,
    view.catalogVersion,
    view.origin,
  )
const assertError = (result, error) => {
  assertJsData(result, error)
  assert.equal(result.ok, false)
  assert.equal(result.identity, null)
  assert.equal(result.error, error)
}

test('WHAT[participant-identity-008] inherited identity requires the exact current owner Persona and version', () => {
  const inherited = identity.inheritParticipantIdentityFromOwner('engineer', 'manager')
  assertJsData(inherited, 'inherited identity')
  assert.equal(inherited.ok, true)
  assert.equal(inherited.error, null)
  assertCanonicalIdentity(
    inherited.identity,
    { ...expectedView('engineer', 'InheritedFromOwner'), persona: 'Lead' },
    'inherited',
  )

  const restored = rehydrate(inherited.identity, 'manager')
  assertJsData(restored, 'rehydrated inherited identity')
  assert.equal(restored.ok, true)
  assertCanonicalIdentity(restored.identity, { ...expectedView('engineer', 'InheritedFromOwner'), persona: 'Lead' }, 'rehydrated inherited')

  assertError(rehydrate(inherited.identity), 'OwnerRequired')
  assertError(
    rehydrate({ ...inherited.identity, persona: 'Director' }, 'manager'),
    'OwnerPersonaMismatch',
  )
  assertError(
    rehydrate({ ...inherited.identity, catalogVersion: 2 }, 'manager'),
    'UnsupportedPersonaCatalogVersion',
  )
  assertError(rehydrate(inherited.identity, 'orchestrator'), 'OwnerPersonaMismatch')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtemp, mkdir, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test, after } = await import("node:test");

const originalHome = process.env.HOME
const home = await mkdtemp(join(tmpdir(), 'wanxiangshu-binding-home-'))
process.env.HOME = home
await mkdir(join(home, '.config', 'opencode'), { recursive: true })
await writeFile(
  join(home, '.config', 'opencode', 'wanxiangshu.mjs'),
  `
export default function route(role) {
  return { model: 'test/deep', reasoning: 'high' }
}
`,
  'utf8',
)
const binding = await import('../../../dist/OpenCode/Host/SessionBindingSurface.js')
const routing = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
await routing.initialize()
const modelFor = (_participant) => ({ providerID: 'test', modelID: 'deep', variant: 'high' })
const assertPrepared = (result, participant) => {
  assert.equal(result.ok, true, result.error)
  assert.equal(result.value.agent, participant)
  assert.equal(result.value.modelProvided, false, 'dispatch remains model-free')
}
const assertNoLegacyRoutingFields = (value, label) => {
  const text = JSON.stringify(value)
  for (const token of ['PeerAgent', 'peerAgent', 'eerAgent', 'EffectiveAgent', 'effectiveAgent', 'ffectiveAgent', 'cursor', 'Cursor']) {
    assert.equal(text.includes(token), false, `${label} must not contain ${token}: ${text}`)
  }
}
const acquireLease = async (sessionId, physicalUserMessageId, role, participant) => {
  const outcome = await routing.acquireSharedExecutionAdmission(
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    null,
  )
  assert.equal(outcome.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(outcome.lease)
  const exact = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target,
  }
  assertNoLegacyRoutingFields(exact, 'admission identity')
  return { lease: outcome.lease, exact }
}
const admitPhysicalExecution = async (sessionId, role, participant) => {
  const physicalId = `msg-binding-${sessionId}`
  const admission = await acquireLease(sessionId, physicalId, role, participant)
  const target = admission.exact.target
  assert.equal(target.model, 'test/deep')
  assert.equal(target.reasoning, 'high')
  assert.deepEqual(
    routing.releaseSharedExecutionAdmissionBeforeProvider(admission.lease, admission.exact),
    { kind: 'Applied' },
  )
  return { target, exact: admission.exact }
}
after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  await rm(home, { recursive: true, force: true })
})

test('WHAT[participant-identity-008] root_requires_external_participant_proof_then_model_is_scheduler_owned', async () => {
  const root = 'ses_binding_root'
  const model = modelFor('engineer')

  const unproven = binding.prepareUserFacing(root, 'engineer', false, model)
  assert.equal(unproven.ok, false)
  assert.match(unproven.error, /no observed user binding/i)

  binding.observeUserFacingAgent(root, 'engineer')
  assertPrepared(binding.prepareUserFacing(root, 'engineer', false, model), 'engineer')
  const first = await admitPhysicalExecution(root, 'engineer', 'engineer')

  const temporary = binding.prepareUserFacing(root, 'engineer', true, modelFor('engineer'))
  assertPrepared(temporary, 'engineer')
  const second = await admitPhysicalExecution(`${root}-override`, 'engineer', 'engineer')

  // Fresh physical retries keep the same fixed participant+Role even as the
  // scheduler hands out a new routing target admission.
  assert.equal(second.exact.participant, first.exact.participant)
  assert.equal(second.exact.role, first.exact.role)
  assert.deepEqual(second.target, first.target)

  // A preserve request cannot use a foreign override as a new base.
  assertPrepared(binding.prepareUserFacing(root, 'engineer', false, model), 'engineer')
  await admitPhysicalExecution(`${root}-restored`, 'engineer', 'engineer')

  const foreign = binding.prepareManaged(root, 'devops', true, modelFor('devops'))
  assert.equal(foreign.ok, false)
  assert.match(foreign.error, /must equal authority participant/i)

  binding.observeUserFacingAgent(root, 'devops')
  assertPrepared(binding.prepareUserFacing(root, 'devops', false, modelFor('devops')), 'devops')
  await admitPhysicalExecution(`${root}-switched`, 'devops', 'devops')

  binding.drop(root)
})
test('WHAT[participant-identity-008] parented_session_uses_stable_participant_lease_and_authorized_peer_only', async () => {
  const parent = 'ses_parent'
  const child = 'ses_child'
  const created = binding.bindChild(parent, child, 'blogger')
  assert.equal(created.ok, true, created.error)

  assertPrepared(binding.prepareManaged(child, 'blogger', false, modelFor('blogger')), 'blogger')
  await admitPhysicalExecution(child, 'blogger', 'blogger')

  const peer = binding.prepareManaged(child, 'blogger', true, modelFor('blogger'))
  assertPrepared(peer, 'blogger')
  await admitPhysicalExecution(`${child}-peer`, 'blogger', 'blogger')

  const foreign = binding.prepareManaged(child, 'engineer', true, modelFor('engineer'))
  assert.equal(foreign.ok, false)
  assert.match(foreign.error, /must equal authority participant/i)

  binding.drop(child)
})
test('WHAT[participant-identity-008] provider_reasoning_variant_must_match_the_exact_lease', async () => {
  const parent = 'ses_variant_parent'
  const child = 'ses_variant_exact'
  assert.equal(binding.bindChild(parent, child, 'devops').ok, true)

  const physicalId = 'msg-variant-exact'
  const expected = modelFor('devops')
  const admission = await acquireLease(child, physicalId, 'devops', 'devops')
  const target = admission.exact.target
  assert.deepEqual(target, { model: 'test/deep', reasoning: 'high' })
  assert.deepEqual(routing.commitSharedExecutionAdmission(admission.lease, admission.exact), { kind: 'Applied' })

  binding.acceptPromptExecution(child, 'prompt-variant-exact', physicalId, 'devops', expected)
  assert.equal(binding.beginProviderAttempt(child, physicalId, 'prompt-variant-exact').ok, true)

  const valid = binding.validateObservedProvider(child, 'devops', expected)
  assert.equal(valid.ok, true)
  assert.equal(valid.value, true)

  const drift = binding.validateObservedProvider(child, 'devops', { ...expected, variant: 'default' })
  assert.equal(drift.ok, false)
  assert.match(drift.error, /model\/reasoning drift/i)
  assert.match(drift.error, /test\/deep\[high\] -> test\/deep\[default\]/)

  binding.drop(child)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");
const roles = await import("../../../dist/Foundation/RolesSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: resolved.identity.name,
      role: resolved.identity.role,
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const syncDelegateRoles = ['Inspector', 'Coder']
assert.deepEqual(syncDelegateRoles, ['Inspector', 'Coder'])

test('WHAT[participant-identity-008] SyncDelegate identity inherits its exact owner Persona and version', () => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-sync-lineage',
    'ses_sync_owner',
    'HumanRoot',
    'msg_sync_owner',
    rootSelection('manager'),
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  const owner = created.value
  const issued = authority.issueInheritedIdentitySeed('engineer', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)

  assert.deepEqual(
    {
      participant: issued.value.participantIdentity.participant,
      role: issued.value.participantIdentity.role,
      persona: issued.value.participantIdentity.persona,
      personaCatalogVersion: issued.value.participantIdentity.personaCatalogVersion,
      ownerSession: issued.value.ownerSession,
      ownerLogicalRun: issued.value.ownerLogicalRun,
      ownerAuthorityRoot: issued.value.ownerAuthorityRoot,
    },
    {
      participant: 'engineer',
      role: 'engineer',
      persona: owner.participantIdentity.persona,
      personaCatalogVersion: owner.participantIdentity.personaCatalogVersion,
      ownerSession: owner.session,
      ownerLogicalRun: owner.logicalRun,
      ownerAuthorityRoot: owner.authorityRoot,
    },
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const Fission = await import("../../../dist/Execution/Fission/Surface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const H = (value) => `H(${value})`
const ownerProfile = (agent = 'engineer') => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  const root = authority.createAuthorityRoot(
    H,
    `runtime-${agent}-profile`,
    `ses_${agent}`,
    'HumanRoot',
    `msg_${agent}`,
    {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: {
        participant: resolved.identity.name,
        role: resolved.identity.role,
        persona: resolved.identity.persona,
        personaCatalogVersion: resolved.identity.catalogVersion,
        origin: resolved.identity.origin,
      },
    },
  )
  assert.equal(root.ok, true, root.ok ? '' : root.error)
  return root.value
}

test('WHAT[participant-identity-008] Strength replica inherits the owner Persona and exact authority lineage', () => {
  const owner = ownerProfile('engineer')
  const issued = authority.issueInheritedIdentitySeed('engineer', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)

  assert.deepEqual(
    {
      ownerSession: issued.value.ownerSession,
      ownerLogicalRun: issued.value.ownerLogicalRun,
      ownerAuthorityRoot: issued.value.ownerAuthorityRoot,
      persona: issued.value.participantIdentity.persona,
      personaCatalogVersion: issued.value.participantIdentity.personaCatalogVersion,
    },
    {
      ownerSession: owner.session,
      ownerLogicalRun: owner.logicalRun,
      ownerAuthorityRoot: owner.authorityRoot,
      persona: owner.participantIdentity.persona,
      personaCatalogVersion: owner.participantIdentity.personaCatalogVersion,
    },
  )
})
test('WHAT[participant-identity-008] Fission lane carries owner-issued identity lineage', () => {
  const owner = ownerProfile('engineer')
  const issued = authority.issueInheritedIdentitySeed('engineer', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)

  assert.deepEqual(authority.validateInheritedIdentitySeed(owner, issued.value), {
    ok: true,
    value: issued.value.participantIdentity,
    error: null,
  })
})
test('WHAT[participant-identity-008] Fission lane identity never infers lineage from a physical parent', () => {
  const lane = Fission.startedLane(1, 'ses_physical_parent', 'investigate independently')
  assert.deepEqual(lane, {
    index: 1,
    prompt: 'investigate independently',
    hasAgentId: false,
    hasHandle: false,
    hasParent: false,
  })
})
}
