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
