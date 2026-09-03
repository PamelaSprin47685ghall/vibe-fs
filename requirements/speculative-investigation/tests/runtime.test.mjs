import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as Fission from '../../../dist/Execution/Fission/Surface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

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
const ownerProfile = (agent = 'coder') => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-special-lineage',
    'ses_special_owner',
    'HumanRoot',
    'msg_special_owner',
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const binding = (owner, replica, decision, role = 'Coder', budget = 'K1') => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, role, budget, 65536, `sem-${decision}`, [])

test('WHAT[SPEC-INV-004] STRENGTH_014_runtime_is_owner_single_flight_and_decision_local', () => {
  const runtime = Strength.runtimeCreate()
  const first = binding('owner', 'replica-1', 'd1')
  const second = binding('owner', 'replica-2', 'd2')
  assert.equal(Strength.runtimeRegister(runtime, first).ok, true)
  const duplicateOwner = Strength.runtimeRegister(runtime, second)
  assert.equal(duplicateOwner.ok, false)
  assert.equal(duplicateOwner.error, 'OwnerAlreadyHasReplica')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-1').decisionId, 'd1')
  assert.equal(Strength.runtimeRetire(runtime, 'replica-1').decisionId, 'd1')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-1'), null)
  assert.equal(Strength.runtimeRegister(runtime, second).ok, true)
})

test('WHAT[SPEC-INV-004] STRENGTH_004_runtime_rejects_unknown_role_and_budget', () => {
  const runtime = Strength.runtimeCreate()
  const unknownRole = Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 'Unknown', 'K1'))
  assert.equal(unknownRole.ok, false)
  assert.match(unknownRole.error, /unknown role/)
  const unknownBudget = Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 'Coder', 'Unknown'))
  assert.equal(unknownBudget.ok, false)
  assert.match(unknownBudget.error, /unknown budget/)
})

test('WHAT[SPEC-INV-004] STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority', () => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 'Coder', 'K0')).error, 'EmptyBudget')
  assert.equal(Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 'Manager', 'K1')).error, 'RoleIneligible')
})

test('WHAT[PID-008] Strength replica inherits the owner Persona and exact authority lineage', () => {
  const owner = ownerProfile('coder')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
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

test('WHAT[PID-008] Fission lane carries owner-issued identity lineage', () => {
  const owner = ownerProfile('coder')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)

  assert.deepEqual(authority.validateInheritedIdentitySeed(owner, issued.value), {
    ok: true,
    value: issued.value.participantIdentity,
    error: null,
  })
})

test('WHAT[PID-008] Fission lane identity never infers lineage from a physical parent', () => {
  const lane = Fission.startedLane(1, 'ses_physical_parent', 'investigate independently')
  assert.deepEqual(lane, {
    index: 1,
    prompt: 'investigate independently',
    hasAgentId: false,
    hasHandle: false,
    hasParent: false,
  })
})
