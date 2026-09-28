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
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
}

const rootSelection = (participant) => {
  const role = participant === 'predictor' ? 'inspector' : participant
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant,
      role,
      selectedTier: 'deep',
      persona: personas[participant] ?? 'Unknown',
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
    rootSelection('engineer'),
  )
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

test('WHAT[dispatch-protocol-006] DP_006_abandon_keeps_the_claim_sequence_consumed', () => {
  const root = profileOf()
  const key = 'pk_x'
  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'BusyAgentNudge', root, 'pd-n'),
    projection,
  )

  const after = authority.abandonClaim(key, projection)

  assert.equal(after.claimSequences.length, 1)
})

test('WHAT[dispatch-protocol-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution', () => {
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.logicalRun,
    promptOrigin('DegenerationGuard'),
    'pd-same',
  )

  let projection = authority.registerAuthority(root, authority.empty)
  assert.equal(authority.nextClaimSequence(scope, projection), 1)

  const claimAt = (n) =>
    authority.claimContinuation(`pk_${n}`, SESSION, 'DegenerationGuard', root, 'pd-same')

  projection = authority.registerClaim(claimAt(1), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authority.abandonClaim('pk_1', projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authority.registerClaim(claimAt(2), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 3)
})

test('WHAT[dispatch-protocol-006] DP_006_same_payload_landings_stay_exact_per_physical_message', () => {
  // Blogger Main dispatches all carry one fixed instruction text: two
  // independent acts, one payload digest. The payload view keeps only the
  // latest landing, and abandoning a later same-payload claim clears that
  // occasion slot; the exact landing of every physical message survives both.
  const root = profileOf()
  const claimAt = (key) => authority.claimContinuation(key, SESSION, 'ManagedDelegationAssignment', root, 'pd-same')

  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(claimAt('pk_1'), projection)
  projection = authority.acceptClaim('pk_1', 'msg_1', projection)
  projection = authority.registerClaim(claimAt('pk_2'), projection)
  projection = authority.acceptClaim('pk_2', 'msg_2', projection)
  projection = authority.registerClaim(claimAt('pk_3'), projection)
  projection = authority.abandonClaim('pk_3', projection)

  const landedAs = (physical) => projection.physicalLandings.find((landing) => landing.physical === physical)
  assert.equal(landedAs('msg_1')?.promptKey, 'pk_1')
  assert.equal(landedAs('msg_2')?.promptKey, 'pk_2')
  assert.equal(landedAs('msg_3'), undefined, 'an abandoned claim never lands')
})
