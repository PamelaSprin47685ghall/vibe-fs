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
  const coderMain = attemptPlan('engineer', 'work-main')
  const coderAgain = attemptPlan('engineer', 'work-main')
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
