import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const exactReadonly = ['Glob', 'Grep', 'Read']

// WHAT[004]: delegation changes the execution purpose, model target, visible
// tools and short-term control. It never creates a second persona.
test('WHAT[speculative-investigation-004] STRENGTH_004_every_role_only_ever_receives_readonly_host_tools', () => {
  for (const role of ['Engineer', 'DevOps', 'Orchestrator', 'Blogger', 'Manager']) {
    const tools = Strength.capabilities(role)
    for (const tool of tools) {
      assert.ok(exactReadonly.includes(tool), `${role} must not receive ${tool}`)
    }
  }
  for (const tool of exactReadonly) {
    assert.equal(Strength.isAllowedTool(tool), true)
  }
  for (const tool of ['Write', 'Edit', 'Bash', 'WebFetch', 'TodoWrite', 'Task', 'read_file']) {
    assert.equal(Strength.isAllowedTool(tool), false, `${tool} is not a readonly delegation tool`)
  }
})
test('WHAT[speculative-investigation-004] STRENGTH_004_unknown_role_fails_closed_without_a_tool_set', () => {
  assert.deepEqual(Strength.capabilities('Unknown'), [])
  assert.deepEqual(
    Strength.exactReadonlyHostToolMap.map((entry) => `${entry.tool}:${entry.allowed}`).sort(),
    ['*:false', 'glob:true', 'grep:true', 'read:true'],
    'the host gate denies everything by wildcard and allows only the three readonly tools',
  )
})
test('WHAT[speculative-investigation-004] STRENGTH_004_replica_never_clears_owner_failure_budget_or_carries_prefix_probe', () => {
  assert.equal(Strength.clearsFailureCountOnSuccess('strength-replica'), false)
  assert.equal(Strength.mayCarryProbe('strength-replica'), false)
  assert.equal(Strength.clearsFailureCountOnSuccess('work-main'), true)
  assert.equal(Strength.mayCarryProbe('work-main'), true)
})
test('WHAT[speculative-investigation-004] STRENGTH_014_replica_is_an_internal_leaf_attached_to_its_owner', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

installDefaultResources()

// WHAT[004]: the delegation narrative calls the helper a companion and never
// introduces it by price or model tier. The owner's own system prompt is not
// rewritten to sell a second worker.
test('WHAT[speculative-investigation-004] STRENGTH_004_007_same_role_prompt_keeps_one_identity_and_no_delegation_advertising', () => {
  const prompt = Strength.systemPromptForRole('Engineer')
  assert.ok(prompt.length > 0)
  assert.equal(Strength.systemPromptIdForRole('Engineer'), Strength.systemPromptIdForRole('Engineer'))
  assert.doesNotMatch(prompt, /\bStrength\b/)
  assert.doesNotMatch(prompt, /replica|prefetch/i)
  assert.doesNotMatch(prompt, /delegate_readonly_rounds|readonly round/i,
    'the delegation protocol belongs to the tool schema and its collaboration text, never to the owner prompt')
  assert.match(prompt, /not necessarily another person/i,
    'the owner prompt keeps the identity law: additional execution is not a second persona')
  assert.doesNotMatch(prompt, /(cheaper|discounted) (model|tier|worker)|companion (worker|pool)|second worker/i,
    'the owner prompt must not introduce the delegation as another person or a cheaper tier')
})
test('WHAT[speculative-investigation-004] STRENGTH_004_role_prompt_identity_is_stable_across_repeated_resolution', () => {
  assert.equal(Strength.systemPromptIdForRole('Orchestrator'), Strength.systemPromptIdForRole('Orchestrator'))
  const prompt = Strength.systemPromptForRole('Orchestrator')
  assert.doesNotMatch(prompt, /replica|prefetch/i)
  assert.doesNotMatch(prompt, /delegate_readonly_rounds|readonly round/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
// A binding boundary never invents a budget: a missing rounds value is an
// empty budget there (Surface roundsResult), so the fixture declares one.
const binding = (owner, replica, decision, rounds = 1, role = 'Engineer') =>
  Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, role, rounds, `sem-${decision}`, [])

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
test('WHAT[speculative-investigation-004] STRENGTH_004_runtime_rejects_unknown_role_and_non_positive_round_budget', () => {
  const runtime = Strength.runtimeCreate()
  const unknownRole = Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 1, 'Unknown'))
  assert.equal(unknownRole.ok, false)
  assert.match(unknownRole.error, /unknown role/i,
    'an undecodable role never reaches the registry as a coded admission decision')
  assert.equal(Strength.capabilities('Unknown').length, 0, 'an unknown role carries no tool set at all')
  const zeroBudget = Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 0))
  assert.equal(zeroBudget.ok, false)
  assert.equal(zeroBudget.error, 'EmptyBudget')
  const negativeBudget = Strength.runtimeRegister(runtime, binding('o3', 'r3', 'd3', -1))
  assert.equal(negativeBudget.ok, false)
  assert.equal(negativeBudget.error, 'EmptyBudget')
})
test('WHAT[speculative-investigation-004] STRENGTH_004_runtime_rejects_roles_without_readonly_capabilities', () => {
  const runtime = Strength.runtimeCreate()
  for (const role of ['Manager', 'Orchestrator', 'Blogger']) {
    const result = Strength.runtimeRegister(runtime, binding('owner', `replica-${role}`, `d-${role}`, 1, role))
    assert.equal(result.ok, false)
    assert.equal(result.error, 'RoleIneligible')
  }
})
}
