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
// A binding boundary has no channel to argue parameters with its caller:
// missing, illegal, or out-of-range declared values are parameter errors
// there (Surface roundsResult). The fixture explicitly declares a valid
// round count so the test exercises domain-layer budget rejection rather
// than boundary-layer parameter rejection.
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
  assert.equal(negativeBudget.error, 'requested-rounds-out-of-range')
})
test('WHAT[speculative-investigation-004] STRENGTH_004_runtime_rejects_roles_without_readonly_capabilities', () => {
  const runtime = Strength.runtimeCreate()
  for (const role of ['Manager', 'Orchestrator', 'Blogger']) {
    const result = Strength.runtimeRegister(runtime, binding('owner', `replica-${role}`, `d-${role}`, 1, role))
    assert.equal(result.ok, false)
    assert.equal(result.error, 'RoleIneligible')
  }
})

test('WHAT[speculative-investigation-004] STRENGTH_004_replica_or_internal_leaf_with_positive_rounds_is_refused_at_admission_without_second_delegation', () => {
  const H = (text) => `H(${text})`
  const baseOpportunity = {
    isRootWork: true,
    requestKind: 'work-main',
    canonicalRole: 'engineer',
    ownerSessionId: 'owner-session',
    ownerLogicalRun: ['logical-1', 'authority-root-1'],
    sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: 'run-1',
    sourceToolCallIds: ['call-1'],
    requestedRounds: 3,
    contractRevision: 2,
    hasPrefixProbe: false,
    isReplicaOrInternalLeaf: false,
    isInteractionRepair: false,
    isExplicitRecoveryBranch: false,
    ownerCancelled: false,
    targetProviderRunBound: true,
    eventStoreHealthy: true,
    hostBoundaryHealthy: true,
    processFuseHealthy: true,
    ownerLogicalRunSuperseded: false,
    pendingRequested: true,
    predictorConfigured: true,
  }

  // Baseline: ordinary owner work with positive rounds is eligible and admitted
  const ownerEligibility = Strength.policyEligibility(baseOpportunity)
  assert.equal(ownerEligibility.kind, 'Eligible')
  const ownerAdmission = Strength.policyDecide(H, baseOpportunity)
  assert.equal(ownerAdmission.kind, 'Admit')
  assert.equal(ownerAdmission.request.requestedRounds, 3)

  // Anti-recursion (H02): Replica or InternalLeaf identity submitting positive rounds
  const replicaOpportunity = {
    ...baseOpportunity,
    isReplicaOrInternalLeaf: true,
  }

  // 1. Eligibility gate check: Ineligible with exact reason 'replica-or-internal-leaf'
  const eligibility = Strength.policyEligibility(replicaOpportunity)
  assert.equal(eligibility.kind, 'Ineligible')
  assert.equal(eligibility.reason, 'replica-or-internal-leaf')

  // 2. Admission decision check: Skip with exact reason, no second delegation request created
  const replicaDecision = Strength.policyDecide(H, replicaOpportunity)
  assert.equal(replicaDecision.kind, 'Skip')
  assert.equal(replicaDecision.reason, 'replica-or-internal-leaf')
  assert.equal('request' in replicaDecision, false, 'Replica must not receive an admitted DelegationRequest')

  // 3. Confirm projection is unaffected: without an admitted request, no DelegationRequested can be created or applied
  const emptyProjection = Strength.projectionEmpty()
  assert.equal(Strength.projectionCandidate('any-decision', emptyProjection), null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const {
  clearAllForTests,
  ensureRoot,
  languageOfSession,
  transformRoleSystem,
  transformReplicaSystem,
  replicaConstraintFor
} = await import("../../../dist/Participant/Provider/LanguageSurface.js");
const { withPreference } = await import("../../provider-language/tests/support/language-fixtures.mjs");

const replicaConstraintZh = "继续当前任务的只读查证，只使用当前可见且获准的工具。信息足够，或下一步需要写入、执行命令、向用户确认、给出结论或作出关键判断时，直接结束，不为继续调用而增加调查。对话里的 self_note 是先前对未来查证的展望，不是已经证实的结论，也不扩大权限。"
const replicaConstraintEn = "Continue the current task's read-only investigation using only the available, permitted tools. Stop when the evidence is sufficient or the next step requires a change, a command, user clarification, a conclusion, or a consequential judgment. Do not invent work to keep calling tools. A self_note in the conversation is an earlier outlook for investigation, not a verified conclusion or permission to do more."

// DELEGATE_REVISE §7.4: Replica additional execution constraint injection and language binding
test('WHAT[speculative-investigation-004] DELEGATE_REVISE_7_4_surface_exports_exact_bilingual_replica_constraints', () => {
  assert.equal(replicaConstraintFor('SimplifiedChinese'), replicaConstraintZh)
  assert.equal(replicaConstraintFor('English'), replicaConstraintEn)
})

test('WHAT[speculative-investigation-004] DELEGATE_REVISE_7_4_replica_system_transform_injects_language_bound_execution_constraint', async () => {
  for (const [preference, language, expectedConstraint] of [
    ['zh-CN', 'SimplifiedChinese', replicaConstraintZh],
    ['en', 'English', replicaConstraintEn],
  ]) {
    await withPreference(preference, async () => {
      clearAllForTests()
      const session = `replica-session-${language}`
      assert.equal(ensureRoot(session), language)
      assert.equal(languageOfSession(session), language)

      const initialSystem = ['Role system segment', 'Host-owned foreign segment']
      const output = await transformReplicaSystem(session, 'Engineer', initialSystem)

      // Injected existence: output.system contains the expected constraint
      assert.ok(Array.isArray(output.system))
      assert.equal(output.system.length, 3)
      assert.equal(output.system[output.system.length - 1], expectedConstraint)
      assert.equal(output.system.includes(expectedConstraint), true)

      // Idempotency: repeated transform with previous output does not duplicate
      const repeated = await transformReplicaSystem(session, 'Engineer', output.system)
      assert.deepEqual(repeated.system, output.system)
      assert.equal(repeated.system.filter(s => s === expectedConstraint).length, 1)
    })
  }
})

test('WHAT[speculative-investigation-004] DELEGATE_REVISE_7_4_non_replica_session_is_strictly_unaffected', async () => {
  for (const [preference, language] of [
    ['zh-CN', 'SimplifiedChinese'],
    ['en', 'English'],
  ]) {
    await withPreference(preference, async () => {
      clearAllForTests()
      const session = `normal-owner-${language}`
      assert.equal(ensureRoot(session), language)

      const initialSystem = ['Role system segment', 'Host-owned foreign segment']
      const output = await transformRoleSystem(session, 'Engineer', initialSystem)

      // Non-replica session must NOT have any replica constraint injected
      assert.equal(output.system.includes(replicaConstraintZh), false)
      assert.equal(output.system.includes(replicaConstraintEn), false)
      assert.equal(output.system.length, 2)
    })
  }
})

test('WHAT[speculative-investigation-004] DELEGATE_REVISE_7_4_replica_system_transform_repairs_constraint_when_session_language_changes', async () => {
  for (const [initial, language, changed, otherLanguage, oldConstraint, newConstraint] of [
    ['en', 'English', 'zh-CN', 'SimplifiedChinese', replicaConstraintEn, replicaConstraintZh],
    ['zh-CN', 'SimplifiedChinese', 'en', 'English', replicaConstraintZh, replicaConstraintEn],
  ]) {
    clearAllForTests()
    const session = `replica-repair-${language}`
    await withPreference(initial, () => assert.equal(ensureRoot(session), language))

    // Initial transform produces old constraint
    const output1 = await transformReplicaSystem(session, 'Engineer', ['Role segment'])
    assert.equal(output1.system.includes(oldConstraint), true)
    assert.equal(output1.system.includes(newConstraint), false)

    // Repeated call keeps old constraint matching bound language
    await withPreference(changed, async () => {
      const outputRepaired = await transformReplicaSystem(session, 'Engineer', output1.system)
      assert.deepEqual(outputRepaired.system, output1.system)
      assert.equal(outputRepaired.system.includes(oldConstraint), true)
      assert.equal(outputRepaired.system.includes(newConstraint), false)
      assert.equal(outputRepaired.system.filter(s => s === oldConstraint).length, 1)
    })
  }
})
}
