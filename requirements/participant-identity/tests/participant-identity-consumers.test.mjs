// WHAT[PID-002/004/005/006/007/008] — canonical participant+Role/Persona/provenance.
//
// Every current assertion uses participant+role. The only old fields in this
// file live inside the explicit raw legacy fixture in the final test, which
// proves the production decoders ignore/drop PeerAgent, EffectiveAgent and
// cursor selection. Current encoders/outputs never contain those fields.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Attempt from '../../../dist/Context/Companion/CompressionSurface.js'
import * as ProviderFailure from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as Dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as Fission from '../../../dist/Execution/Fission/Surface.js'
import * as Authority from '../../../dist/Interaction/Authority/Surface.js'
import * as Runtime from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as Persona from '../../../dist/Participant/Persona/Surface.js'

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

test('WHAT[PID-005] provider planning selects the system prompt and tool set from profile Role', () => {
  const coderMain = attemptPlan('coder', 'work-main')
  const coderAgain = attemptPlan('coder', 'work-main')
  const devops = attemptPlan('devops', 'work-main')

  assert.equal(coderMain.systemPromptId, coderMain.participantIdentity.role)
  assert.equal(coderAgain.systemPromptId, coderMain.systemPromptId)
  assert.deepEqual(coderAgain.toolCapabilities, coderMain.toolCapabilities)
  assert.equal(devops.systemPromptId, devops.participantIdentity.role)
  assert.equal(Authority.systemPromptIdForRole(coderMain.participantIdentity.role), coderMain.systemPromptId)
  assert.equal(Authority.systemPromptIdForRole(devops.participantIdentity.role), devops.systemPromptId)
  assert.notDeepEqual(devops.toolCapabilities, coderMain.toolCapabilities)
  assert.equal(coderMain.toolCapabilities.includes('Write'), true)
  assert.equal(devops.toolCapabilities.includes('Exec'), true)
})

test('WHAT[PID-002] ProviderAttempt carries its ParticipantIdentity as one nested value', () => {
  const attempt = attemptPlan('inspector', 'work-main')
  const expected = canonicalIdentityOf('inspector')

  assert.equal(attempt.participant, expected.participant)
  assert.equal(attempt.role, expected.role)
  assertCanonicalIdentity(attempt.participantIdentity, expected, 'attempt.participantIdentity')
  assertNoLegacyIdentityFields(attempt, 'attemptPlan')
})

test('WHAT[PID-006] fresh physical retries preserve ParticipantIdentity while the failure budget advances', () => {
  const first = attemptPlan('coder', 'work-main')
  const expected = canonicalIdentityOf('coder')

  // Fresh physical provider runs for the same fixed Role resolve the exact
  // same participant identity; only the ProviderFailureBudget moves.
  let projection = ProviderFailure.providerFailureProjection.forAuthority('run-identity-retry', 'root-identity-retry')
  assert.equal(ProviderFailure.providerFailureProjection.mayRetry(ProviderFailure.budget.defaultBudget, projection), true)

  const providerRuns = ['provider-run-1', 'provider-run-2', 'provider-run-3']
  let count = 0
  for (const providerRun of providerRuns) {
    count += 1
    const retry = attemptPlan('coder', 'work-main')
    assertCanonicalIdentity(retry.participantIdentity, expected, `retry ${providerRun}`)

    const applied = ProviderFailure.providerFailureProjection.applyFailure(
      { session: 'ses-identity-retry', run: 'run-identity-retry', root: 'root-identity-retry', attempt: providerRun },
      count,
      projection,
    )
    assert.equal(applied.ok, true, `failure ${providerRun} applies`)
    projection = applied.value
  }

  const read = ProviderFailure.providerFailureProjection.read(projection)
  assert.equal(read.failures, providerRuns.length)
  assert.equal(read.exhausted, false)

  const last = attemptPlan('coder', 'work-main')
  assertCanonicalIdentity(last.participantIdentity, expected, 'post-retry identity')
})

test('WHAT[PID-006] durable provider failure fold preserves the exact IdentitySeed', () => {
  const canonical = canonicalIdentityOf('coder')
  const seed = {
    ...rootSelection('coder'),
    participantIdentity: {
      ...canonical,
      selectedAgent: canonical.participant,
      canonicalRole: canonical.role,
    },
  }
  const root = ProviderFailure.authorityRootAccepted({
    session: 'ses-identity-fold',
    logicalRun: 'run-identity-fold',
    authorityRoot: 'msg-identity-fold',
    authorityKind: 'HumanRoot',
    identitySeed: seed,
  })
  const failures = [1, 2].map((consecutiveFailureCount, index) =>
    ProviderFailure.providerFailureRecorded({
      session: 'ses-identity-fold',
      logicalRun: 'run-identity-fold',
      authorityRoot: 'msg-identity-fold',
      providerRun: `provider-run-fold-${index + 1}`,
      consecutiveFailureCount,
      reason: 'provider_error',
    }),
  )
  const folded = ProviderFailure.fold([
    ProviderFailure.envelope({ session: 'ses-identity-fold', seq: 1, fact: root, providerRun: null }),
    ...failures.map((fact, index) =>
      ProviderFailure.envelope({
        session: 'ses-identity-fold',
        seq: index + 2,
        fact,
        providerRun: `provider-run-fold-${index + 1}`,
      }),
    ),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const read = ProviderFailure.providerFailureProjection.read(folded.value)
  assert.equal(read.failures, 2)
  assert.equal(read.exhausted, false)
  assertNoLegacyIdentityFields(root, 'authorityRootAccepted')
})

test('WHAT[PID-004] terminal dispatch preserves the exact IdentitySeed', () => {
  const profile = rootProfile('devops')
  const promptKey = 'pk_identity_terminal_dispatch'
  const claim = Runtime.claimContinuation(
    promptKey,
    profile.session,
    'BusyAgentNudge',
    profile,
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

test('WHAT[PID-008] child identity inherits the parent Persona and version across roles', () => {
  const parent = rootProfile('coder', 'ses_identity_parent')
  const child = inheritedSeed('inspector', parent)

  assert.equal(child.participantIdentity.participant, 'inspector')
  assert.equal(child.participantIdentity.role, 'inspector')
  assert.deepEqual(personaVersion(child.participantIdentity), personaVersion(parent.participantIdentity))
  assert.equal(child.ownerSession, parent.session)
  assert.equal(child.ownerLogicalRun, parent.logicalRun)
  assert.equal(child.ownerAuthorityRoot, parent.authorityRoot)
})

test('WHAT[PID-004] Strength replica inherits owner Persona and version with the same participant', () => {
  const owner = rootProfile('coder', 'ses_identity_strength_owner')
  const replica = inheritedSeed('coder', owner)

  assert.equal(replica.participantIdentity.participant, owner.participantIdentity.participant)
  assert.equal(replica.participantIdentity.role, owner.participantIdentity.role)
  assert.equal(
    attemptPlan('coder', 'work-main').participantIdentity.participant,
    replica.participantIdentity.participant,
  )
  assert.deepEqual(personaVersion(replica.participantIdentity), personaVersion(owner.participantIdentity))
  assert.equal(Strength.systemPromptIdForRole(replica.participantIdentity.role), 'coder')
  assert.deepEqual(
    new Set(Strength.readonlyCapabilities(replica.participantIdentity.role, 'strength-replica')),
    new Set(['Read', 'Glob', 'Grep']),
  )
})

test('WHAT[PID-004] Fission lane inherits owner Persona and version without physical-parent inference', () => {
  const owner = rootProfile('inspector', 'ses_identity_fission_owner')
  const laneIdentity = inheritedSeed('inspector', owner)
  const lane = Fission.startedLane(2, 'ses_unrelated_physical_parent', 'inspect lane')

  assert.equal(laneIdentity.participantIdentity.participant, 'inspector')
  assert.equal(laneIdentity.participantIdentity.role, owner.participantIdentity.role)
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

test('WHAT[PID-007] Bookkeeper has private identity and no public Role', () => {
  const bookkeeper = Persona.resolveParticipantIdentityAtRoot('bookkeeper')

  assert.equal(bookkeeper.ok, true, bookkeeper.ok ? '' : bookkeeper.error)
  assert.equal(bookkeeper.identity.name, 'bookkeeper')
  assert.equal(bookkeeper.identity.role, 'bookkeeper')
  assert.notEqual(bookkeeper.identity.persona, '')
  assert.equal(Number.isInteger(bookkeeper.identity.catalogVersion), true)
  assert.equal(Persona.allPublicRoleLabels.includes(bookkeeper.identity.role), false)
  assert.equal(Persona.allRoleLabels.includes(bookkeeper.identity.role), false)
})

test('WHAT[PID-004] raw legacy PeerAgent/EffectiveAgent/cursor fields are ignored and never re-encoded', () => {
  const canonical = canonicalIdentityOf('coder')
  const legacySeed = {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      ...canonical,
      peerAgent: 'coder',
      PeerAgent: 'coder',
      effectiveAgent: 'coder',
      EffectiveAgent: 'coder',
    },
    cursor: { offset: 2, failures: 2 },
    effectiveAgent: 'coder',
  }

  const profile = rootProfile('coder', 'ses_identity_legacy_drop')
  const accepted = Runtime.createAuthorityRoot(
    hash,
    'runtime-participant-identity-consumers',
    'ses_identity_legacy_drop',
    'HumanRoot',
    'msg_identity_legacy_drop',
    legacySeed,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)
  assertCanonicalIdentity(accepted.value.participantIdentity, canonical, 'legacy-dropped identity')
  assertNoLegacyIdentityFields(accepted.value, 'accepted legacy-dropped profile')
  assert.deepEqual(accepted.value.participantIdentity, profile.participantIdentity)

  const plan = attemptPlan('coder', 'work-main')
  assertNoLegacyIdentityFields(plan, 'attemptPlan output')
})
