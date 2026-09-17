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

test('WHAT[PID-006] fresh physical retries preserve ParticipantIdentity while the failure budget advances', () => {
  const first = attemptPlan('engineer', 'work-main')
  const expected = canonicalIdentityOf('engineer')

  // Fresh physical provider runs for the same fixed Role resolve the exact
  // same participant identity; only the ProviderFailureBudget moves.
  let projection = ProviderFailure.providerFailureProjection.forAuthority('run-identity-retry', 'root-identity-retry')
  assert.equal(ProviderFailure.providerFailureProjection.mayRetry(ProviderFailure.budget.defaultBudget, projection), true)

  const providerRuns = ['provider-run-1', 'provider-run-2', 'provider-run-3']
  let count = 0
  for (const providerRun of providerRuns) {
    count += 1
    const retry = attemptPlan('engineer', 'work-main')
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

  const last = attemptPlan('engineer', 'work-main')
  assertCanonicalIdentity(last.participantIdentity, expected, 'post-retry identity')
})

test('WHAT[PID-006] durable provider failure fold preserves the exact IdentitySeed', () => {
  const canonical = canonicalIdentityOf('engineer')
  const seed = {
    ...rootSelection('engineer'),
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
