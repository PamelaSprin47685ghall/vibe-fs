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

test('WHAT[SPEC-INV-011] STRENGTH_015_replica_semantic_vs_physical_tail_lifecycle_split', () => {
  const runtime = Strength.runtimeCreate()
  const b = binding('owner-life', 'replica-life', 'd-life')
  assert.equal(Strength.runtimeRegister(runtime, b).ok, true)

  // Business decision resolves, replica is in live registry
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life').decisionId, 'd-life')

  // Exact physical tail cleanup removes it from live registry
  const retired = Strength.runtimeRetire(runtime, 'replica-life')
  assert.equal(retired.decisionId, 'd-life')

  // Once retired, presence is gone and business cannot restart from registry presence
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life'), null)
})

const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const replicaBinding = (owner, replica, decision, budget) => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Coder', budget, 65536, `sem-${decision}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const attach = (replica, budget, purpose = 'Treatment', owner = 'owner') => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const decision = `decision-${replica}`
  const result = Strength.replicaAttach(handle, replicaBinding(owner, replica, decision, budget), purpose)
  assert.equal(result.ok, true, result.error)
  return { handle, completion: result.value.completion }
}
const turn = (sessionId, outcome, providerRun = 'run-t') => ({ sessionId, providerRun, outcome, parts: [] })
const oneBatch = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] })

test('WHAT[SPEC-INV-011] STRENGTH_015_replica_semantic_terminal_is_first_wins_and_physical_tail_cannot_restart_business', async () => {
  const { handle, completion } = attach('replica-sem', 'K1')
  // The K gate retires semantic admission but the physical identity lives on.
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-sem')), true)
  const admitted = Strength.replicaPeek(handle, 'replica-sem')
  assert.equal(admitted.terminal.kind, 'BudgetReached')
  assert.ok(admitted.requestsAdmitted <= 1)
  // A duplicate terminal through the turn path is consumed as physical tail
  // only: the first outcome never changes.
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'failed')), true)
  const outcome = await Strength.replicaAwaitOutcome(completion)
  assert.equal(outcome.terminal.kind, 'BudgetReached')
  assert.ok(outcome.requestsAdmitted <= 1)
  // Retired: no peek, no live binding, no restart, one lease release.
  assert.equal(Strength.replicaPeek(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaLiveFind(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaIsReplica(handle, 'replica-sem'), false)
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'completed')), false)
  assert.deepEqual(Strength.replicaReleased(handle), ['replica-sem'])
})

test('WHAT[SPEC-INV-003] STRENGTH_003_replica_request_counts_never_exceed_the_immutable_k_budget', async () => {
  // K1 with a two-batch transcript still reports at most one request.
  const k1 = attach('replica-k1c', 'K1')
  const two = { messages: [user('u1', 'replica-k1c', [hostText('Continue.')]), assistant('a1', 'replica-k1c', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]), assistant('a2', 'replica-k1c', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')])] }
  assert.equal(await Strength.replicaHandleTransform(k1.handle, two), true)
  assert.equal(Strength.replicaPeek(k1.handle, 'replica-k1c').requestsAdmitted, 1)
  assert.deepEqual(Strength.replicaReleased(k1.handle), [])
  // K2 with a three-batch transcript still reports at most two requests.
  const k2 = attach('replica-k2c', 'K2')
  const three = { messages: [user('u1', 'replica-k2c', [hostText('Continue.')]), assistant('a1', 'replica-k2c', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]), assistant('a2', 'replica-k2c', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]), assistant('a3', 'replica-k2c', [hostResult('c3', 'glob', { pattern: '**/*.fs' }, 'a.fs')])] }
  assert.equal(await Strength.replicaHandleTransform(k2.handle, three), true)
  assert.equal(Strength.replicaPeek(k2.handle, 'replica-k2c').requestsAdmitted, 2)
  const outcome = await Strength.replicaAwaitOutcome(k2.completion)
  assert.equal(outcome.terminal.kind, 'BudgetReached')
  assert.equal(outcome.requestsAdmitted, 2)
})

test('WHAT[SPEC-INV-011] STRENGTH_015_session_delete_retires_live_and_orphan_bindings_with_one_lease_release', () => {
  // Live decision state: delete retires peek, binding and lease exactly once.
  const live = attach('replica-del', 'K1', 'Treatment', 'owner-del')
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.equal(Strength.replicaPeek(live.handle, 'replica-del'), null)
  assert.equal(Strength.replicaLiveFind(live.handle, 'replica-del'), null)
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])
  // Orphan binding with no local decision state: delete still retires and releases.
  const orphan = Strength.replicaRuntimeCreate(65536)
  assert.equal(Strength.replicaLiveRegister(orphan, replicaBinding('owner-orph', 'replica-orph', 'dec-orph', 'K1')).ok, true)
  assert.equal(Strength.replicaPeek(orphan, 'replica-orph'), null)
  Strength.replicaSessionDeleted(orphan, 'replica-orph')
  assert.equal(Strength.replicaLiveFind(orphan, 'replica-orph'), null)
  assert.deepEqual(Strength.replicaReleased(orphan), ['replica-orph'])
  // Owner deletion cascades to its live replica.
  const owned = attach('replica-owned', 'K1', 'Treatment', 'owner-owned')
  Strength.replicaSessionDeleted(owned.handle, 'owner-owned')
  assert.equal(Strength.replicaPeek(owned.handle, 'replica-owned'), null)
  assert.equal(Strength.replicaLiveFind(owned.handle, 'replica-owned'), null)
  assert.deepEqual(Strength.replicaReleased(owned.handle), ['replica-owned'])
})

test('WHAT[SPEC-INV-011] STRENGTH_015_replica_dispose_keeps_first_terminal_and_clears_all_live_resources', async () => {
  // Dispose before any terminal completes the open decision as Cancelled.
  const open = attach('replica-open', 'K1')
  Strength.replicaDispose(open.handle)
  const cancelled = await Strength.replicaAwaitOutcome(open.completion)
  assert.equal(cancelled.terminal.kind, 'Cancelled')
  assert.equal(Strength.replicaPeek(open.handle, 'replica-open'), null)
  assert.equal(Strength.replicaLiveFind(open.handle, 'replica-open'), null)
  assert.deepEqual(Strength.replicaReleased(open.handle), ['replica-open'])
  // Dispose after a terminal keeps the first terminal fixed.
  const closed = attach('replica-closed', 'K1')
  assert.equal(Strength.replicaHandleTurn(closed.handle, turn('replica-closed', 'completed')), true)
  const first = await Strength.replicaAwaitOutcome(closed.completion)
  assert.equal(first.terminal.kind, 'TextCompleted')
  Strength.replicaDispose(closed.handle)
  const kept = await Strength.replicaAwaitOutcome(closed.completion)
  assert.deepEqual(kept, first)
  assert.deepEqual(Strength.replicaReleased(closed.handle), ['replica-closed'])
})

test('WHAT[SPEC-INV-013] STRENGTH_013_dry_run_closes_only_at_the_exact_owner_target_run', async () => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const attached = Strength.replicaAttach(handle, replicaBinding('owner-dry', 'replica-dry', 'dec-dry', 'K1'), 'DryRun')
  assert.equal(attached.ok, true, attached.error)
  // A different run never closes the observation.
  await Strength.replicaCloseDryRun(handle, turn('owner-dry', 'completed', 'run-other'))
  assert.equal(Strength.replicaPeek(handle, 'replica-dry').terminal, null)
  // The exact owner target run closes it as Cancelled.
  await Strength.replicaCloseDryRun(handle, turn('owner-dry', 'completed', 'run-dec-dry'))
  const outcome = await Strength.replicaAwaitOutcome(attached.value.completion)
  assert.equal(outcome.terminal.kind, 'Cancelled')
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
