import assert from 'node:assert/strict'
import test from 'node:test'

import * as Attempt from '../../../dist/Context/Companion/CompressionSurface.js'
import * as Fallback from '../../../dist/Participant/Provider/Attempt/Fallback/CursorSurface.js'
import * as Dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as Fission from '../../../dist/Execution/Fission/Surface.js'
import * as Authority from '../../../dist/Interaction/Authority/Surface.js'
import * as Runtime from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as Persona from '../../../dist/Participant/Persona/Surface.js'

const hash = (value) => `H(${value})`

const rootSelection = (agent) => {
  const resolved = Persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.peer,
      canonicalRole: resolved.identity.role,
      selectedTier: resolved.identity.initialTier.toLowerCase(),
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}

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

const attemptPlan = (role, tier, offset = 0, kind = 'work-main') =>
  Attempt.attemptPlan({
    role,
    tier,
    kind,
    cursor: { offset, failures: offset },
    noCandidateReason: 'NoCoverage',
  })

const inheritedSeed = (childAgent, owner) => {
  const issued = Runtime.issueInheritedIdentitySeed(childAgent, owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return issued.value
}

const personaVersion = (identity) => ({
  persona: identity.persona,
  personaCatalogVersion: identity.personaCatalogVersion,
})

test('WHAT[PID-005] provider planning selects the system prompt and tool set from profile Role', () => {
  const fastCoder = attemptPlan('coder', 'fast')
  const deepCoder = attemptPlan('coder', 'deep')
  const reviewer = attemptPlan('reviewer', 'fast')

  assert.equal(fastCoder.systemPromptId, fastCoder.participantIdentity.canonicalRole)
  assert.equal(deepCoder.systemPromptId, fastCoder.systemPromptId)
  assert.deepEqual(deepCoder.toolCapabilities, fastCoder.toolCapabilities)
  assert.equal(reviewer.systemPromptId, reviewer.participantIdentity.canonicalRole)
  assert.equal(Authority.systemPromptIdForRole(fastCoder.participantIdentity.canonicalRole), fastCoder.systemPromptId)
  assert.equal(Authority.systemPromptIdForRole(reviewer.participantIdentity.canonicalRole), reviewer.systemPromptId)
  assert.notDeepEqual(reviewer.toolCapabilities, fastCoder.toolCapabilities)
  assert.equal(fastCoder.toolCapabilities.includes('Write'), true)
  assert.equal(reviewer.toolCapabilities.includes('Judge'), true)
})

test('WHAT[PID-002] ProviderAttempt carries its ParticipantIdentity as one nested value', () => {
  const attempt = attemptPlan('inspector', 'deep')
  const resolved = Persona.resolveParticipantIdentityAtRoot('deep-inspector')

  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  assert.deepEqual(attempt.participantIdentity, {
    selectedAgent: resolved.identity.name,
    peerAgent: resolved.identity.peer,
    canonicalRole: resolved.identity.role,
    selectedTier: resolved.identity.initialTier.toLowerCase(),
    persona: resolved.identity.persona,
    personaCatalogVersion: resolved.identity.catalogVersion,
    origin: resolved.identity.origin,
  })
})

test('WHAT[PID-006] fallback preserves ParticipantIdentity', () => {
  const selected = attemptPlan('coder', 'fast', 0)
  const fallback = attemptPlan('coder', 'fast', 2)
  const pair = {
    selectedAgent: selected.participantIdentity.selectedAgent,
    peerAgent: selected.participantIdentity.peerAgent,
  }

  assert.equal(Fallback.cursor.effectiveAgent(pair, { offset: 0, failures: 0 }), selected.effectiveAgent)
  assert.equal(Fallback.cursor.effectiveAgent(pair, { offset: 2, failures: 2 }), fallback.effectiveAgent)
  assert.notEqual(fallback.effectiveAgent, selected.effectiveAgent)
  assert.deepEqual(fallback.participantIdentity, selected.participantIdentity)
})

test('WHAT[PID-004] terminal dispatch preserves the exact IdentitySeed', () => {
  const profile = rootProfile('deep-devops')
  const promptKey = 'pk_identity_terminal_dispatch'
  const claim = Runtime.claimContinuation(
    promptKey,
    profile.session,
    'BusyAgentNudge',
    profile,
    profile.participantIdentity.peerAgent,
    'digest-identity-terminal-dispatch',
  )
  let projection = Runtime.registerAuthority(profile, Runtime.empty)
  projection = Runtime.registerClaim(claim, projection)
  projection = Runtime.acceptClaim(promptKey, 'msg_identity_terminal_dispatch', projection)

  assert.equal(Dispatch.sendMemberObservation().owner, 'PromptDispatcher.Runtime')
  assert.equal(projection.pendingClaims.length, 0)
  assert.equal(projection.acceptedDispatches.length, 1)
  assert.deepEqual(projection.acceptedDispatches[0].identitySeed, profile.identitySeed)
})

test('WHAT[PID-010] child identity inherits the parent Persona and version across role and tier', () => {
  const parent = rootProfile('fast-coder', 'ses_identity_parent')
  const child = inheritedSeed('deep-reviewer', parent)

  assert.equal(child.participantIdentity.selectedTier, 'deep')
  assert.equal(child.participantIdentity.canonicalRole, 'reviewer')
  assert.deepEqual(personaVersion(child.participantIdentity), personaVersion(parent.participantIdentity))
  assert.equal(child.ownerSession, parent.session)
  assert.equal(child.ownerLogicalRun, parent.logicalRun)
  assert.equal(child.ownerAuthorityRoot, parent.authorityRoot)
})

test('WHAT[PID-004] Strength replica inherits owner Persona and version with a different EffectiveAgent', () => {
  const owner = rootProfile('fast-coder', 'ses_identity_strength_owner')
  const replica = inheritedSeed('deep-coder', owner)

  assert.notEqual(replica.participantIdentity.selectedAgent, owner.participantIdentity.selectedAgent)
  assert.deepEqual(personaVersion(replica.participantIdentity), personaVersion(owner.participantIdentity))
  assert.equal(Strength.systemPromptIdForRole(replica.participantIdentity.canonicalRole), 'coder')
  assert.deepEqual(
    new Set(Strength.readonlyCapabilities(replica.participantIdentity.canonicalRole, 'strength-replica')),
    new Set(['Read', 'Glob', 'Grep']),
  )
})

test('WHAT[PID-004] Fission lane inherits owner Persona and version without physical-parent inference', () => {
  const owner = rootProfile('deep-inspector', 'ses_identity_fission_owner')
  const laneIdentity = inheritedSeed('fast-inspector', owner)
  const lane = Fission.startedLane(2, 'ses_unrelated_physical_parent', 'inspect lane')

  assert.notEqual(laneIdentity.participantIdentity.selectedTier, owner.participantIdentity.selectedTier)
  assert.deepEqual(personaVersion(laneIdentity.participantIdentity), personaVersion(owner.participantIdentity))
  assert.equal(laneIdentity.ownerLogicalRun, owner.logicalRun)
  assert.deepEqual(lane, {
    index: 2,
    prompt: 'inspect lane',
    hasAgentId: false,
    hasHandle: false,
    hasParent: false,
  })
})

test('WHAT[PID-009] Bookkeeper has private identity and no public Role', () => {
  const bookkeeper = Persona.resolveParticipantIdentityAtRoot('fast-bookkeeper')

  assert.equal(bookkeeper.ok, true, bookkeeper.ok ? '' : bookkeeper.error)
  assert.equal(bookkeeper.identity.name, 'fast-bookkeeper')
  assert.equal(bookkeeper.identity.peer, 'deep-bookkeeper')
  assert.notEqual(bookkeeper.identity.persona, '')
  assert.equal(Number.isInteger(bookkeeper.identity.catalogVersion), true)
  assert.equal(Persona.allPublicRoleLabels.includes(bookkeeper.identity.role), false)
  assert.equal(Persona.allRoleLabels.includes(bookkeeper.identity.role), false)
})
