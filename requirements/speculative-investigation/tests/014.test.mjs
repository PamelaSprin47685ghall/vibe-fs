import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const ablation = await import("../../../dist/Ablation/Surface.js");

const H = (text) => createHash('sha256').update(text).digest('hex')
const configured = {
  isRootWork: true, requestKind: 'work-main', canonicalRole: 'engineer', ownerSessionId: 'owner',
  ownerLogicalRun: ['logical-1', 'authority-root-1'], sourcePhysicalUserMessageId: 'user-1',
  sourceProviderRun: 'run-1', sourceToolCallIds: ['call-1'], requestedRounds: 1, contractRevision: 1,
  hasPrefixProbe: false, isReplicaOrInternalLeaf: false, isInteractionRepair: false, isExplicitRecoveryBranch: false,
  ownerCancelled: false, targetProviderRunBound: true, eventStoreHealthy: true, hostBoundaryHealthy: true,
  processFuseHealthy: true, ownerLogicalRunSuperseded: false, pendingRequested: true, predictorConfigured: true,
}

test('WHAT[speculative-investigation-014] STRENGTH_014_configured_and_unconfigured_predictor_are_two_visible_states', () => {
  assert.equal(Strength.policyEligibility(configured).kind, 'Eligible')
  const unconfigured = Strength.policyEligibility({ ...configured, predictorConfigured: false })
  assert.equal(unconfigured.kind, 'Ineligible')
  assert.equal(unconfigured.reason, 'predictor-unconfigured')
})
test('WHAT[speculative-investigation-014] STRENGTH_014_external_feature_registration_is_not_a_second_enable_condition', (t) => {
  const previous = process.env.WANXIANGSHU_STRENGTH_MODE
  t.after(() => {
    if (previous === undefined) delete process.env.WANXIANGSHU_STRENGTH_MODE
    else process.env.WANXIANGSHU_STRENGTH_MODE = previous
    ablation.resetRegistry()
  })
  // Whatever the ablation registry says about this feature, the delegation
  // admission still follows only the model configuration.
  const expected = Strength.policyDecide(H, configured)
  assert.equal(expected.kind, 'Admit')
  process.env.WANXIANGSHU_STRENGTH_MODE = 'treatment'
  assert.deepEqual(Strength.policyDecide(H, configured), expected)
  assert.equal(Strength.policyDecide(H, { ...configured, predictorConfigured: false }).reason, 'predictor-unconfigured')
})
test('WHAT[speculative-investigation-014] STRENGTH_014_removing_the_configuration_only_closes_unbound_requests', () => {
  let projection = Strength.projectionEmpty()
  const apply = (event) => {
    const result = Strength.projectionApply(projection, event)
    assert.equal(result.ok, true, result.error)
    projection = result.value
  }
  apply(Strength.eventRequested({
    decisionId: 'd1', ownerSessionId: 'owner',
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
    sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
  }))
  // An authorization that was never bound closes explicitly instead of drifting
  // for ever once its enabling configuration is removed.
  projection = Strength.projectionApply(projection, Strength.eventClosed('d1', 'Requested', 'CannotContinue')).value
  assert.equal(Strength.projectionCandidate('d1', projection).state, 'Closed')
  assert.equal(Strength.projectionRequestedRounds('d1', projection), 2)
})
test('WHAT[speculative-investigation-014] STRENGTH_014_a_bound_execution_keeps_its_target_and_ends_by_its_own_rules', () => {
  let projection = Strength.projectionEmpty()
  const apply = (event) => {
    const result = Strength.projectionApply(projection, event)
    assert.equal(result.ok, true, result.error)
    projection = result.value
  }
  apply(Strength.eventRequested({
    decisionId: 'd2', ownerSessionId: 'owner',
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-2',
    sourceToolCallIds: ['call-2'], requestedRounds: 1, contractRevision: 1,
  }))
  apply(Strength.eventBound('d2', 'run-2', 'replica-d2', 'anchor-b'))
  // Configuration removal does not re-open, re-target or shrink the bound work.
  assert.equal(Strength.projectionDecisionForTarget('run-2', projection), 'd2')
  assert.equal(Strength.projectionRequestedRounds('d2', projection), 1)
  const rebound = Strength.projectionApply(projection, Strength.eventBound('d2', 'run-2', 'replica-other', 'anchor-c'))
  assert.equal(rebound.ok, false)
})
test('WHAT[speculative-investigation-014] STRENGTH_014_the_contract_revision_is_code_stable_not_environment_driven', () => {
  const withEnv = (name, value, run) => {
    const previous = process.env[name]
    try {
      if (value === undefined) delete process.env[name]
      else process.env[name] = value
      run()
    } finally {
      if (previous === undefined) delete process.env[name]
      else process.env[name] = previous
    }
  }
  const base = Strength.policyDecide(H, configured).request.contractRevision
  for (const name of ['WANXIANGSHU_CONTRACT_REVISION', 'WANXIANGSHU_STRENGTH_MODE', 'WANXIANGSHU_STRENGTH_CONTRACT']) {
    for (const value of ['1', '2', '0', 'latest', '']) {
      withEnv(name, value, () => assert.equal(Strength.policyDecide(H, configured).request.contractRevision, base))
    }
  }
})
}
