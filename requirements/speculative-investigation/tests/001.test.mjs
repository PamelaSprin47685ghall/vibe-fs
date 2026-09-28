import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

installDefaultResources()
const opportunity = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'engineer',
  ownerSessionId: 'owner',
  ownerLogicalRun: ['logical-1', 'authority-root-1'],
  sourcePhysicalUserMessageId: 'user-1',
  sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1', 'call-2'],
  requestedRounds: 2,
  contractRevision: 1,
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

// WHAT[001]: delegation is visible only once the owner model offered integers
// through the tool protocol. Nothing in this Surface can enable, disable or
// adjust that from the host side.
test('WHAT[speculative-investigation-001] STRENGTH_001_strength_surface_exposes_no_enable_switch_or_rollout_mode', () => {
  for (const name of ['settingsLoad', 'settingsDryRunBudget', 'settingsHostCanaryHealthy', 'settingsHostCanaryFingerprint', 'startDryRun', 'observeDryRun', 'closeDryRunAtPrimaryTerminal']) {
    assert.equal(Strength[name], undefined, `no host-side switch may remain exported as ${name}`)
  }
  for (const name of ['predictorCreate', 'rolloutEstimate', 'costEstimate']) {
    assert.equal(Strength[name], undefined, `no statistical estimator may remain exported as ${name}`)
  }
})
test('WHAT[speculative-investigation-001] STRENGTH_002_011_unconfigured_predictor_is_a_visible_dependency_gap_not_a_silent_baseline', () => {
  assert.equal(Strength.policyEligibility({ ...opportunity, predictorConfigured: false }).reason, 'predictor-unconfigured')
  assert.equal(Strength.policyEligibility(opportunity).kind, 'Eligible')
})
test('WHAT[speculative-investigation-001] STRENGTH_001_014_zero_rounds_and_unconfigured_predictor_are_two_observably_different_states', () => {
  const zero = Strength.policyDecide((text) => text, { ...opportunity, requestedRounds: 0 })
  const unconfigured = Strength.policyDecide((text) => text, { ...opportunity, predictorConfigured: false })
  assert.equal(zero.kind, 'Skip')
  assert.equal(zero.reason, 'zero-round-budget')
  assert.equal(unconfigured.kind, 'Skip')
  assert.equal(unconfigured.reason, 'predictor-unconfigured')
  assert.notEqual(zero.reason, unconfigured.reason)
})
test('WHAT[speculative-investigation-001] STRENGTH_002_speculation_opportunity_is_pure_frozen_protocol_evidence_without_wall_clock_state', () => {
  const first = Strength.policyDecide((text) => text, opportunity)
  const second = Strength.policyDecide((text) => text, opportunity)
  assert.deepEqual(first, second)
  assert.equal(first.kind, 'Admit')
  assert.equal(first.request.requestedRounds, 2)
})
}

test.todo('WHAT[speculative-investigation-001] GAP-183: compare actual Work execution with optimization absent, Off, fused and K0 across provider bytes, permissions, retry and finality')
