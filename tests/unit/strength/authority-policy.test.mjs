import assert from 'node:assert/strict'
import test from 'node:test'

import {
  ProviderRequestKind,
  ProviderRequestKindModule_clearsFailureCountOnSuccess as clearsFailureCountOnSuccess,
  ProviderRequestKindModule_mayCarryProbe as mayCarryProbe,
} from '../../../dist/Domain/PrefixCandidate.js'
import * as Cost from '../../../dist/Domain/StrengthCostModel.js'
import * as Policy from '../../../dist/Domain/StrengthPolicy.js'
import { toolCapabilitiesFor } from '../../../dist/Domain/PromptAuthority.js'
import { AgentTier, Role } from '../../../dist/Kernel/Roles.js'

const caseName = (value) => value.cases()[value.tag]
const permissionNames = (set) => [...set].map(caseName).sort()

const exactReadonly = ['Glob', 'Grep', 'Read']

for (const role of [Role.Coder, Role.Inspector, Role.DevOps, Role.Meditator]) {
  test(`STRENGTH_004_${caseName(role)}_replica_has_exact_readonly_capabilities`, () => {
    assert.deepEqual(permissionNames(toolCapabilitiesFor(role, ProviderRequestKind.StrengthReplica)), exactReadonly)
  })
}

for (const role of [Role.Manager, Role.Orchestrator, Role.Browser, Role.Reviewer, Role.Executor, Role.Blogger]) {
  test(`STRENGTH_004_${caseName(role)}_replica_is_fail_closed`, () => {
    assert.deepEqual(permissionNames(toolCapabilitiesFor(role, ProviderRequestKind.StrengthReplica)), [])
  })
}

test('STRENGTH_004_019_replica_is_never_owner_fallback_or_prefix_probe_evidence', () => {
  assert.equal(clearsFailureCountOnSuccess(ProviderRequestKind.StrengthReplica), false)
  assert.equal(mayCarryProbe(ProviderRequestKind.StrengthReplica), false)
  assert.equal(clearsFailureCountOnSuccess(ProviderRequestKind.WorkMain), true)
  assert.equal(mayCarryProbe(ProviderRequestKind.WorkMain), true)
})

test('STRENGTH_010_value_equations_charge_fast_bytes_delay_and_risk', () => {
  assert.equal(typeof Cost.StrengthCostModel_estimate, 'function')

  const estimate = Cost.StrengthCostModel_estimate(
    0.8, // P1
    0.5, // P2
    10, // SavedDeep1
    8, // SavedDeep2
    2, // Fast1
    1, // Fast2
    0.5, // Byte1
    0.9, // Byte2
    0.25, // Delay1
    0.4, // Delay2
    0.75, // Risk1
    1.1, // Risk2
  )

  assert.equal(estimate.V0, 0)
  assert.equal(estimate.V1, 0.8 * 10 - 2 - 0.5 - 0.25 - 0.75)
  assert.equal(
    estimate.V2,
    0.8 * 10 + 0.8 * 0.5 * 8 - 2 - 0.8 * 1 - 0.9 - 0.4 - 1.1,
  )
})

test('STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities', () => {
  assert.equal(typeof Policy.StrengthPolicy_decideFromFacts, 'function')

  const base = {
    IsRootWork: true,
    RequestKind: ProviderRequestKind.WorkMain,
    CanonicalRole: Role.Coder,
    SelectedTier: AgentTier.Deep,
    SelectedAgent: 'deep-coder',
    EffectiveAgent: 'deep-coder',
    IsFallbackRetry: false,
    HasPrefixProbe: false,
    IsReviewerOrFinality: false,
    IsAttachedOrInternalLeaf: false,
    OwnerCancelled: false,
    TargetProviderRunBound: true,
    EventStoreHealthy: true,
    HostCanaryHealthy: true,
    FastPeerAvailable: true,
    ModelBindingsDistinct: true,
    CostModelAvailable: true,
  }

  const prediction = { P1: 0.9, P2: 0.8, EvidenceCount: 100 }
  const values = { V0: 0, V1: 5, V2: 8 }
  const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }

  assert.equal(caseName(Policy.StrengthPolicy_decideFromFacts(base, false, false, prediction, values, config)), 'Speculate')
  assert.equal(caseName(Policy.StrengthPolicy_decideFromFacts({ ...base, EffectiveAgent: 'fast-coder' }, false, false, prediction, values, config)), 'Skip')
  assert.equal(caseName(Policy.StrengthPolicy_decideFromFacts({ ...base, CostModelAvailable: false }, false, false, prediction, values, config)), 'Skip')
  assert.equal(caseName(Policy.StrengthPolicy_decideFromFacts(base, true, false, prediction, values, config)), 'ControlHoldout')
  assert.equal(caseName(Policy.StrengthPolicy_decideFromFacts(base, false, true, prediction, values, config)), 'Skip')
})
