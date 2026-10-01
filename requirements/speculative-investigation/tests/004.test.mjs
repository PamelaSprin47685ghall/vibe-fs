import test from 'node:test'

{
  const { default: assert } = await import('node:assert/strict')
  const { replicaFixture, error, ok } = await import('./support/replica-lifecycle-fixture.mjs')

  test('WHAT[speculative-investigation-004] concurrent preparation claims owner before creating a predictor', async () => {
    let finishCreate
    const pendingCreate = new Promise(resolve => { finishCreate = resolve })
    const fixture = replicaFixture({ create: () => pendingCreate })
    const first = fixture.prepare('decision-one')
    const second = fixture.prepare('decision-two')
    finishCreate()
    const results = await Promise.all([first, second])
    assert.equal(results.filter(result => result.ok).length, 1)
    assert.equal(fixture.children.length, 1, 'busy owner never creates an extra predictor session')
    assert.deepEqual(fixture.aborted, [], 'no loser child needs cleanup')
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] a reused predictor ignores the previous decision terminal and retires its observer', async () => {
    const fixture = replicaFixture()
    const first = await fixture.prepare('decision-one')
    assert.equal(first.ok, true)
    const replica = first.value.replicaSessionId
    await fixture.admit(replica, 'physical-one')
    assert.equal(fixture.terminal(replica), false, 'finished decision detaches its terminal observer')
    await first.value.completion
    const second = await fixture.prepare('decision-two')
    assert.equal(second.ok, true)
    assert.equal(fixture.isReplica(replica), true, 'old sticky terminal cannot close the new decision')
    assert.equal(fixture.children.length, 1)
    await fixture.admit(replica, 'physical-two')
    fixture.observe(replica, 'physical-one', 'response-physical-one')
    assert.equal(fixture.isReplica(replica), true, 'late old physical turn cannot close a later decision')
    fixture.terminal(replica)
    await second.value.completion
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] duplicate preparation of one decision shares a single child and completion', async () => {
    let finishCreate
    const pendingCreate = new Promise(resolve => { finishCreate = resolve })
    const fixture = replicaFixture({ create: () => pendingCreate })
    const first = fixture.prepare('same-decision')
    const repeated = fixture.prepare('same-decision')
    finishCreate()
    const [a, b] = await Promise.all([first, repeated])
    assert.equal(a.ok, true)
    assert.equal(b.ok, true)
    assert.equal(a.value.completion, b.value.completion)
    assert.equal(fixture.children.length, 1)
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] duplicate consumers retain the exact outcome across physical cleanup until publication acknowledges it', async () => {
    const fixture = replicaFixture()
    const prepared = await fixture.prepare('first-decision')
    const replica = prepared.value.replicaSessionId
    await fixture.admit(replica, 'physical-first')
    fixture.terminal(replica)
    assert.equal(fixture.isReplica(replica), false)
    const retained = fixture.tryOutcome(replica, 'first-decision')
    assert.ok(retained)
    const outcome = await fixture.awaitOutcome(retained)
    assert.equal(outcome.requestsAdmitted, 1)
    assert.equal(outcome.terminal.kind, 'TextCompleted', 'first decision naturally completed')
    assert.equal(fixture.tryOutcome(replica, 'other-decision'), null)
    const next = await fixture.prepare('next-decision')
    assert.equal(next.ok, true)
    await fixture.admit(replica, 'physical-next')
    fixture.observe(replica, 'physical-first', 'response-physical-first')
    assert.equal(fixture.isReplica(replica), true)
    assert.equal((await fixture.awaitOutcome(retained)).terminal.kind, 'TextCompleted')
    fixture.releaseOutcome('first-decision')
    assert.equal(fixture.tryOutcome(replica, 'first-decision'), null)
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] owner cancellation drains an in-flight creation before any decision can run', async () => {
    let finishCreate
    const pendingCreate = new Promise(resolve => { finishCreate = resolve })
    const fixture = replicaFixture({ create: () => pendingCreate })
    const pending = fixture.prepare('cancelled-decision')
    const cancelled = fixture.cancelOwner()
    finishCreate()
    const result = await pending
    await cancelled
    assert.equal(result.ok, false)
    assert.match(result.error, /owner ended during preparation/)
    assert.deepEqual(fixture.aborted, ['replica-1'])
    assert.equal(fixture.isReplica(fixture.children[0].sessionId), false)
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] an unloaded coordinator cannot be revived by a late preparation callback', async () => {
    const fixture = replicaFixture()
    fixture.dispose()
    const result = await fixture.prepare('after-disposal')
    assert.equal(result.ok, false)
    assert.match(result.error, /disposed/)
    assert.deepEqual(fixture.children, [])
  })

  test('WHAT[speculative-investigation-004] unavailable model capacity closes an unsent decision without poisoning later owner admission', async () => {
    const fixture = replicaFixture({ acquire: () => undefined })
    const prepared = await fixture.prepare('unavailable-capacity')
    const replica = prepared.value.replicaSessionId
    const sent = await fixture.sendPrepared(replica)
    assert.equal(sent.ok, false)
    assert.match(sent.error, /model-capacity-unavailable/)
    assert.equal((await fixture.awaitOutcome(prepared.value.completion)).requestsAdmitted, 0)
    assert.equal(fixture.isReplica(replica), false, 'unsent work has no physical terminal to await')
    const next = await fixture.prepare('next-opportunity')
    assert.equal(next.ok, true)
    assert.equal(fixture.children.length, 1)
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] failed resident verification refuses replacement instead of multiplying sessions', async () => {
    let unavailable = false
    const fixture = replicaFixture({ list: children => unavailable ? error('host-unavailable') : ok(children) })
    const first = await fixture.prepare('decision-one')
    assert.equal(first.ok, true)
    await fixture.admit(first.value.replicaSessionId, 'physical-one')
    fixture.terminal(first.value.replicaSessionId)
    await first.value.completion
    unavailable = true
    const second = await fixture.prepare('decision-two')
    assert.equal(second.ok, false)
    assert.match(second.error, /host-unavailable/)
    assert.equal(fixture.children.length, 1, 'query error is not evidence of permanent child loss')
    fixture.dispose()
  })

  test('WHAT[speculative-investigation-004] early proven completion is retained until physical binding and prevents a late extra request', async () => {
    const messagesByReplica = new Map()
    const fixture = replicaFixture({
      getMessages: replica => messagesByReplica.get(replica) || [],
    })
    const prepared = await fixture.prepare('decision-early-complete')
    assert.equal(prepared.ok, true)
    const replica = prepared.value.replicaSessionId

    // Old physical turn or old completion arriving before physical binding must not terminate current decision
    messagesByReplica.set(replica, [
      { info: { id: 'old-run', role: 'assistant', sessionID: replica, parentID: 'stale-physical' }, parts: [] },
    ])
    fixture.notifyCompleted(replica, 'old-run')
    fixture.observe(replica, 'stale-physical', 'old-run')
    await new Promise(resolve => setImmediate(resolve))
    assert.equal(fixture.isReplica(replica), true, 'stale physical completion does not terminate decision')

    // Current decision's completed physical arrives via event notification before first transform
    messagesByReplica.set(replica, [
      { info: { id: 'old-run', role: 'assistant', sessionID: replica, parentID: 'stale-physical' }, parts: [] },
      { info: { id: 'early-run', role: 'assistant', sessionID: replica, parentID: 'physical-early' }, parts: [] },
    ])
    fixture.notifyCompleted(replica, 'early-run')
    await new Promise(resolve => setImmediate(resolve))
    assert.equal(fixture.isReplica(replica), true, 'early proven completed is buffered before transform binding')

    // Binding replays the terminal before this late transform can admit more work.
    await fixture.admit(replica, 'physical-early')

    // It must now deliver the buffered completed turn and transition to terminal
    assert.equal(fixture.isReplica(replica), false, 'replayed early terminal closes live replica')
    const outcome = await fixture.awaitOutcome(prepared.value.completion)
    assert.equal(outcome.terminal.kind, 'TextCompleted')
    assert.equal(outcome.requestsAdmitted, 0, 'terminal replay must not admit a request that will never be sent')

    // Repeated completion notification does not contaminate or re-deliver
    fixture.notifyCompleted(replica, 'early-run')
    assert.equal(fixture.isReplica(replica), false)

    fixture.dispose()
  })
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const exactReadonly = ['Glob', 'Grep', 'Read']
const allowedTools = ['read', 'glob', 'grep', 'js-predictor']

// WHAT[004]: delegation changes the execution purpose, model target, visible
// tools and short-term control. It never creates a second persona.
test('WHAT[speculative-investigation-004] STRENGTH_004_every_role_only_ever_receives_readonly_host_tools', () => {
  for (const role of ['Engineer', 'DevOps', 'Orchestrator', 'Blogger', 'Manager']) {
    assert.deepEqual(Strength.capabilities(role), exactReadonly, `${role} receives exactly the readonly capability set`)
  }
  for (const tool of allowedTools) {
    assert.equal(Strength.isAllowedTool(tool), true)
  }
  for (const tool of ['Write', 'Edit', 'Bash', 'WebFetch', 'TodoWrite', 'Task', 'read_file', 'js-coder']) {
    assert.equal(Strength.isAllowedTool(tool), false, `${tool} is not a readonly delegation tool`)
  }
})
test('WHAT[speculative-investigation-004] STRENGTH_004_unknown_role_fails_closed_without_a_tool_set', () => {
  assert.deepEqual(Strength.capabilities('Unknown'), [])
  assert.deepEqual(
    Strength.exactReadonlyHostToolMap.map((entry) => `${entry.tool}:${entry.allowed}`).sort(),
    ['*:false', 'js-predictor:true'],
    'the host gate denies everything by wildcard and allows exactly the single readonly JS surface',
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
  for (const role of ['Coder', 'Inspector', 'Browser', 'Inquiry', 'Distiller']) {
    const result = Strength.runtimeRegister(runtime, binding('owner', `replica-${role}`, `d-${role}`, 1, role))
    assert.equal(result.ok, false)
    assert.equal(result.error, 'RoleIneligible')
  }
  for (const role of ['Manager', 'Orchestrator', 'Engineer', 'DevOps', 'Blogger']) {
    const result = Strength.runtimeRegister(runtime, binding(`owner-${role}`, `replica-${role}`, `d-${role}`, 1, role))
    assert.equal(result.ok, true, `${role} role is eligible for StrengthReplica with exact readonly capabilities`)
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

test('WHAT[speculative-investigation-004] replica_readonly_constraint_replica_system_transform_injects_language_bound_execution_constraint', async () => {
  for (const [preference, language, expectedConstraint] of [
    ['zh-CN', 'SimplifiedChinese', replicaConstraintFor('SimplifiedChinese')],
    ['en', 'English', replicaConstraintFor('English')],
  ]) {
    await withPreference(preference, async () => {
      clearAllForTests()
      const session = `replica-session-${language}`
      assert.equal(ensureRoot(session), language)
      assert.equal(languageOfSession(session), language)

      const initialSystem = ['Role system segment', 'Host-owned foreign segment']
      const output = await transformReplicaSystem(session, 'Engineer', initialSystem)

      assert.deepEqual(initialSystem, ['Role system segment', 'Host-owned foreign segment', expectedConstraint],
        'the Host retains its original system array; the execution constraint must reach that buffer')
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

test('WHAT[speculative-investigation-004] replica_readonly_constraint_non_replica_session_is_strictly_unaffected', async () => {
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
      assert.equal(output.system.includes(replicaConstraintFor('SimplifiedChinese')), false)
      assert.equal(output.system.includes(replicaConstraintFor('English')), false)
      assert.equal(output.system.length, 2)
    })
  }
})

test('WHAT[speculative-investigation-004] replica_readonly_constraint_replica_system_transform_repairs_constraint_when_session_language_changes', async () => {
  for (const [initial, language, changed, otherLanguage, oldConstraint, newConstraint] of [
    ['en', 'English', 'zh-CN', 'SimplifiedChinese', replicaConstraintFor('English'), replicaConstraintFor('SimplifiedChinese')],
    ['zh-CN', 'SimplifiedChinese', 'en', 'English', replicaConstraintFor('SimplifiedChinese'), replicaConstraintFor('English')],
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
