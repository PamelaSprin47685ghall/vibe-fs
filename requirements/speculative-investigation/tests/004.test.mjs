import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const exactReadonly = ['Glob', 'Grep', 'Read']
const base = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'coder',
  selectedAgent: 'coder',
  hasPrefixProbe: false,
  isAttachedOrInternalLeaf: false,
  ownerCancelled: false,
  targetProviderRunBound: true,
  eventStoreHealthy: true,
  hostCanaryHealthy: true,
  predictorAvailable: true,
  costModelAvailable: true,
}
const prediction = { P1: 0.9, P2: 0.8, evidenceCount: 100 }
const values = { V0: 0, V1: 5, V2: 8 }
const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }

test('WHAT[speculative-investigation-004] STRENGTH_004_each_supported_replica_has_exact_readonly_capabilities', () => {
  for (const role of ['Coder', 'Inspector', 'DevOps', 'Inquiry']) {
    assert.deepEqual(Strength.capabilities(role), exactReadonly)
  }
})
test('WHAT[speculative-investigation-004] STRENGTH_004_each_unsupported_replica_is_fail_closed', () => {
  for (const role of ['Manager', 'Orchestrator', 'Browser', 'Reviewer', 'Distiller', 'Blogger']) {
    assert.deepEqual(Strength.capabilities(role), [])
  }
})
test('WHAT[speculative-investigation-004] STRENGTH_004_019_replica_never_clears_owner_failure_budget_or_carries_prefix_probe', () => {
  assert.equal(Strength.clearsFailureCountOnSuccess('strength-replica'), false)
  assert.equal(Strength.mayCarryProbe('strength-replica'), false)
  assert.equal(Strength.clearsFailureCountOnSuccess('work-main'), true)
  assert.equal(Strength.mayCarryProbe('work-main'), true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

installDefaultResources()
const eligibleOpportunity = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'coder',
  selectedAgent: 'coder',
  effectiveAgent: 'coder',
  isFallbackRetry: false,
  hasPrefixProbe: false,
  isReviewerOrFinality: false,
  isAttachedOrInternalLeaf: false,
  ownerCancelled: false,
  targetProviderRunBound: true,
  eventStoreHealthy: true,
  hostCanaryHealthy: true,
  predictorAvailable: true,
  costModelAvailable: true,
}
const prediction = { P1: 0.9, P2: 0.8, evidenceCount: 100 }
const values = { V0: 0, V1: 5, V2: 8 }
const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }
const decide = (opportunity, control = false, shadow = false, p = prediction, v = values, c = config) => Strength.policyDecide(opportunity, control, shadow, p, v, c)
const skipReason = (decision) => {
  assert.equal(decision.kind, 'Skip')
  return decision.reason
}

test('WHAT[speculative-investigation-004] STRENGTH_014_policy_strength_replica_is_internal_leaf_attached_not_satellite_kind', () => {
  const facts = Strength.associationFacts('owner-work')
  assert.deepEqual(facts.satelliteCases, ['Companion'])
  assert.equal(facts.hasReplicaSatellite, false)
  assert.equal(facts.attachmentCases.includes('StrengthReplica'), true)
  assert.equal(facts.executionClass, 'InternalLeaf')
  assert.equal(facts.ownerSessionId, 'owner-work')
  assert.equal(facts.attachment, 'StrengthReplica')
  assert.equal(facts.strengthReplicaAttachment, true)
  assert.equal(facts.companionAttachment, false)
})
test('WHAT[speculative-investigation-004] STRENGTH_004_007_policy_same_role_prompt_has_no_replica_identity', () => {
  const engId = Strength.systemPromptIdForRole('Engineer')
  assert.equal(engId, Strength.systemPromptIdForRole('Engineer'))
  const prompt = Strength.systemPromptForRole('Engineer')
  assert.ok(prompt.length > 0)
  assert.doesNotMatch(prompt, /\bStrength\b/)
  assert.doesNotMatch(prompt, /replica|prefetch/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostCall = (callId, tool, input) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output: 'pending' } })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })
const binding = (replica, budget) => Strength.runtimeBinding('owner', replica, `decision-${replica}`, `target-${replica}`, 'Coder', budget, 65536, `semantic-${replica}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const registered = (replica, budget) => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding(replica, budget)).ok, true)
  return runtime
}
const apply = async (runtime, output) => Strength.transformApply(H, runtime, output)

test('WHAT[speculative-investigation-004] STRENGTH_004_non_replica_session_returns_not_replica', async () => {
  const runtime = registered('replica-known', 'K1')
  const output = { messages: [user('u1', 'unknown-session', [hostText('Continue.')])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'NotReplica')
  assert.deepEqual(outcome.batches, [])
  assert.deepEqual(outcome.aborted, [])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Fission = await import("../../../dist/Execution/Fission/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

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
const ownerProfile = (agent = 'engineer') => {
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

test('WHAT[speculative-investigation-004] STRENGTH_014_runtime_is_owner_single_flight_and_decision_local', () => {
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
test('WHAT[speculative-investigation-004] STRENGTH_004_runtime_rejects_unknown_role_and_budget', () => {
  const runtime = Strength.runtimeCreate()
  const unknownRole = Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 'Unknown', 'K1'))
  assert.equal(unknownRole.ok, false)
  assert.match(unknownRole.error, /unknown role/)
  const unknownBudget = Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 'Coder', 'Unknown'))
  assert.equal(unknownBudget.ok, false)
  assert.match(unknownBudget.error, /unknown budget/)
})
test('WHAT[speculative-investigation-004] STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority', () => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 'Coder', 'K0')).error, 'EmptyBudget')
  assert.equal(Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 'Manager', 'K1')).error, 'RoleIneligible')
})
}
