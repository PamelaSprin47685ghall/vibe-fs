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

test('WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity', () => {
  const admission = 'accepted-1a2b'
  const physical = 'msg_real'
  assert.equal(admission, 'accepted-1a2b')
  assert.equal(physical, 'msg_real')
  assert.equal(authority.transportReceiptShape(admission), true, 'accepted-* is admission shape')
  assert.equal(authority.transportReceiptShape(physical), false, 'msg_* is not admission shape')
})
