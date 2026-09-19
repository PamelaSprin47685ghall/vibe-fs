import test from 'node:test'

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

test('WHAT[speculative-investigation-001] STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven', () => {
  const unhealthy = decide({ ...eligibleOpportunity, hostCanaryHealthy: false })
  assert.equal(skipReason(unhealthy), 'host-canary-unhealthy')
  assert.equal(unhealthy.budget, 'K0')
  const noPredictor = decide({ ...eligibleOpportunity, predictorAvailable: false })
  assert.equal(skipReason(noPredictor), 'predictor-unavailable')
  assert.equal(noPredictor.budget, 'K0')
  const noCost = decide({ ...eligibleOpportunity, costModelAvailable: false })
  assert.equal(skipReason(noCost), 'cost-model-unavailable')
  assert.equal(noCost.budget, 'K0')
  const shadow = decide(eligibleOpportunity, false, true)
  assert.equal(skipReason(shadow), 'shadow-k0')
  assert.equal(shadow.budget, 'K0')
})
test('WHAT[speculative-investigation-001] STRENGTH_001_014_policy_nested_replica_cannot_speculate', () => {
  const nested = decide({ ...eligibleOpportunity, isRootWork: false, isAttachedOrInternalLeaf: true, requestKind: 'strength-replica' })
  assert.equal(nested.budget, 'K0')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const packageJson = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))
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
const withCanary = (value, run) => withEnv('WANXIANGSHU_STRENGTH_HOST_CANARY', value, run)

test('WHAT[speculative-investigation-001] STRENGTH_011_default_settings_are_shadow_k0_with_economic_holdout_and_no_k2_enablement', () => {
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => {
    withCanary(undefined, () => {
      const settings = Strength.settingsLoad()
      assert.equal(settings.mode, 'Shadow')
      assert.equal(settings.costs, null)
      assert.equal(Strength.settingsHostCanaryHealthy(), false)
      assert.equal(settings.controlRateBasisPoints, 1000)
      assert.equal(settings.policy.K2MinimumEvidence, 50)
      assert.ok(settings.policy.K2Margin > settings.policy.K1Margin)
    })
  })
})
}
