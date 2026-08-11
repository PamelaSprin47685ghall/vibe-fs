// tests/unit/strength/host-canary-k0.test.mjs
//
// Phase 0 / §二十四 Host canary inventory at the policy/transform API.
// These are failing-closed unit proofs. They are NOT live Host / LLM canaries:
// nested provider-loop deadlock, permission popups, ReviewSeal-over-final-bytes,
// and OpenCode version-upgrade re-runs still require a real Host.
//
// Honest status: G8 remains PARTIAL. This file does not enable K1/K2 and does
// not skip the economic control holdout.

import assert from 'node:assert/strict'
import test from 'node:test'

import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Policy from '../../../dist/Domain/StrengthPolicy.js'
import { StrengthBudget, StrengthBudgetModule_requestLimit as requestLimit } from '../../../dist/Domain/StrengthBudget.js'
import { toolCapabilitiesFor, systemPromptIdFor } from '../../../dist/Domain/PromptAuthority.js'
import * as Runtime from '../../../dist/Session/StrengthRuntime.js'
import * as Association from '../../../dist/Journal/SessionAssociation.js'
import { SatelliteKind } from '../../../dist/Journal/SessionAssociation.js'
import { AttachmentKind, SessionExecutionClass } from '../../../dist/Kernel/SessionOwnership.js'
import { AgentTier, Role, RoleDefinitions_promptFor, ToolPermission } from '../../../dist/Kernel/Roles.js'
import { SystemPromptIdModule_value as promptIdValue } from '../../../dist/Kernel/Identity.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { toArray as mapEntries } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'

const caseOf = (value) => value.cases()[value.tag]
const permissionNames = (set) => [...set].map(caseOf).sort()

const eligibleOpportunity = {
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

const decide = (opportunity, control = false, shadow = false) =>
  Policy.StrengthPolicy_decideFromFacts(opportunity, control, shadow, prediction, values, config)

const skipReason = (decision) => {
  assert.equal(caseOf(decision), 'Skip')
  return decision.fields[0]
}

test('STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven', () => {
  // Not the live Host upgrade canary. Policy-level: unknown canary/cost → K0.
  const unhealthy = decide({ ...eligibleOpportunity, HostCanaryHealthy: false })
  assert.equal(skipReason(unhealthy), 'host-canary-unhealthy')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(unhealthy)), 'K0')

  const noCost = decide({ ...eligibleOpportunity, CostModelAvailable: false })
  assert.equal(skipReason(noCost), 'cost-model-unavailable')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(noCost)), 'K0')

  const shadow = decide(eligibleOpportunity, false, true)
  assert.equal(skipReason(shadow), 'shadow-k0')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(shadow)), 'K0')
})

test('STRENGTH_002_013_review_finality_and_attached_internal_leaf_are_always_k0', () => {
  // Not the live ReviewSeal-over-final-bytes Host canary. Policy-level: these
  // surfaces cannot speculate even when every other fact is green.
  const reviewer = decide({
    ...eligibleOpportunity,
    CanonicalRole: Role.Reviewer,
    SelectedAgent: 'deep-reviewer',
    EffectiveAgent: 'deep-reviewer',
  })
  assert.equal(skipReason(reviewer), 'role-ineligible')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(reviewer)), 'K0')

  const finalityFlag = decide({ ...eligibleOpportunity, IsReviewerOrFinality: true })
  assert.equal(skipReason(finalityFlag), 'review-or-finality')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(finalityFlag)), 'K0')

  const attached = decide({ ...eligibleOpportunity, IsAttachedOrInternalLeaf: true })
  assert.equal(skipReason(attached), 'attached-or-internal-leaf')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(attached)), 'K0')

  const notRoot = decide({ ...eligibleOpportunity, IsRootWork: false, IsAttachedOrInternalLeaf: true })
  assert.equal(skipReason(notRoot), 'not-root-work')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(notRoot)), 'K0')
})

test('STRENGTH_002_003_target_unbound_and_replica_request_kind_are_k0', () => {
  // Not the live Host TargetProviderRun binding canary. Policy-level fail-closed.
  const unbound = decide({ ...eligibleOpportunity, TargetProviderRunBound: false })
  assert.equal(skipReason(unbound), 'target-provider-run-unbound')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(unbound)), 'K0')

  const replicaKind = decide({ ...eligibleOpportunity, RequestKind: ProviderRequestKind.StrengthReplica })
  assert.equal(skipReason(replicaKind), 'not-work-main')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(replicaKind)), 'K0')
})

test('STRENGTH_010_economic_holdout_is_not_skipped_and_ineligible_never_counts_as_holdout', () => {
  const holdout = decide(eligibleOpportunity, true, false)
  assert.equal(caseOf(holdout), 'ControlHoldout')
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(holdout)), 'K0')

  const ineligibleHoldout = decide({ ...eligibleOpportunity, HostCanaryHealthy: false }, true, false)
  assert.equal(skipReason(ineligibleHoldout), 'host-canary-unhealthy')
  assert.notEqual(caseOf(ineligibleHoldout), 'ControlHoldout')
})

test('STRENGTH_010_k2_is_gated_and_not_enabled_by_this_proof', () => {
  // Gate only. This is not a K2 DONE claim and does not turn K2 on.
  const belowFloor = Policy.StrengthPolicy_decideFromFacts(
    eligibleOpportunity,
    false,
    false,
    { ...prediction, EvidenceCount: 19 },
    values,
    config,
  )
  assert.equal(caseOf(belowFloor), 'Speculate')
  assert.equal(caseOf(belowFloor.fields[0]), 'K1')

  const equalMargin = Policy.StrengthPolicy_decideFromFacts(
    eligibleOpportunity,
    false,
    false,
    prediction,
    values,
    { K1Margin: 1, K2Margin: 1, K2MinimumEvidence: 20 },
  )
  assert.equal(caseOf(equalMargin), 'Speculate')
  assert.equal(caseOf(equalMargin.fields[0]), 'K1')
})

test('STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network', () => {
  // Not the live Host execution-gate canary. Same capability set the schema is
  // built from: forged mutating/network/session tools stay outside the replica.
  const capabilities = toolCapabilitiesFor(Role.Coder, ProviderRequestKind.StrengthReplica)
  const allowed = new Set(permissionNames(capabilities))
  const denied = [
    ToolPermission.Write,
    ToolPermission.Edit,
    ToolPermission.Exec,
    ToolPermission.Fork,
    ToolPermission.Join,
    ToolPermission.List,
    ToolPermission.Network,
    ToolPermission.Pty,
  ]
  for (const permission of denied) {
    assert.equal(allowed.has(caseOf(permission)), false, caseOf(permission))
  }

  for (const tool of ['write', 'edit', 'executor', 'fork', 'join', 'network', 'bash']) {
    assert.equal(Frame.StrengthFrame_isAllowedTool(tool), false, tool)
  }
  assert.equal(Frame.StrengthFrame_isAllowedTool('read'), true)
  assert.equal(Frame.StrengthFrame_isAllowedTool('glob'), true)
  assert.equal(Frame.StrengthFrame_isAllowedTool('grep'), true)
})

test('STRENGTH_004_006_policy_replica_host_tool_map_denies_unknown_tools_instead_of_asking', () => {
  // Not the live Host permission-popup canary. `* = false` is the unit stand-in:
  // unknown tools are denied, so there is no permission-ask surface to raise.
  const entries = Object.fromEntries(mapEntries(Runtime.StrengthReplicaTools_exactReadonlyHostToolMap))
  assert.equal(entries['*'], false)
  assert.equal(entries.read, true)
  assert.equal(entries.glob, true)
  assert.equal(entries.grep, true)
})

test('STRENGTH_004_007_policy_same_role_prompt_has_no_replica_identity', () => {
  // Not the live Host system-prompt canary. Domain prompt id is CanonicalRole
  // only; replica request kind cannot introduce a Strength identity string.
  const coderId = promptIdValue(systemPromptIdFor(Role.Coder))
  assert.equal(coderId, promptIdValue(systemPromptIdFor(Role.Coder)))
  assert.doesNotMatch(coderId, /strength|replica|prefetch/i)

  const prompt = RoleDefinitions_promptFor(Role.Coder)
  assert.doesNotMatch(prompt, /strength|replica|prefetch/i)
  assert.match(prompt, /Coder/)
})

test('STRENGTH_014_policy_strength_replica_is_internal_leaf_attached_not_satellite_kind', () => {
  assert.deepEqual(SatelliteKind.Companion.cases(), ['Companion'])
  assert.equal('Replica' in SatelliteKind, false)
  assert.equal('StrengthReplica' in SatelliteKind, false)
  assert.equal(SatelliteKind.Companion.cases().includes('Replica'), false)

  assert.ok(AttachmentKind.Companion.cases().includes('StrengthReplica'))
  assert.equal(Association.StrengthReplicaAssociationHints_executionClass, SessionExecutionClass.InternalLeaf)

  const owner = Id.SessionIdModule_create('owner-work')
  const ownership = Association.StrengthReplicaAssociationHints_ownership(owner)
  assert.equal(caseOf(ownership), 'Attached')
  assert.equal(Id.SessionIdModule_value(ownership.fields[0]), 'owner-work')
  assert.equal(caseOf(ownership.fields[1]), 'StrengthReplica')
  assert.equal(Association.StrengthReplicaAssociationHints_isStrengthReplicaAttachment(AttachmentKind.StrengthReplica), true)
  assert.equal(Association.StrengthReplicaAssociationHints_isStrengthReplicaAttachment(AttachmentKind.Companion), false)
})

test('STRENGTH_001_014_policy_nested_replica_cannot_speculate_this_is_not_the_live_deadlock_canary', () => {
  // Nested session safety on a live Host (Work transform waiting on the
  // InternalLeaf provider/tool loop without deadlock) is still blocked.
  // This only proves the replica surface is ineligible to start another Strength.
  const nested = decide({
    ...eligibleOpportunity,
    IsRootWork: false,
    IsAttachedOrInternalLeaf: true,
    RequestKind: ProviderRequestKind.StrengthReplica,
  })
  assert.equal(caseOf(Policy.StrengthPolicy_budgetOf(nested)), 'K0')
  assert.equal(requestLimit(StrengthBudget.K0), 0)
})
