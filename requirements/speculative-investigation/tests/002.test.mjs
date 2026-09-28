import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const base = {
  isRootWork: true, requestKind: 'work-main', canonicalRole: 'devops', selectedAgent: 'devops',
  hasPrefixProbe: false, isAttachedOrInternalLeaf: false, ownerCancelled: false,
  targetProviderRunBound: true, eventStoreHealthy: true, hostCanaryHealthy: true,
  predictorAvailable: true, costModelAvailable: true,
}
const prediction = { P1: 0.9, P2: 0.8, evidenceCount: 100 }
const values = { V0: 0, V1: 5, V2: 8 }
const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }
const decide = opportunity => Strength.policyDecide(opportunity, false, false, prediction, values, config)

test('WHAT[speculative-investigation-002] policy rejects every individually unproven opportunity prerequisite', () => {
  assert.equal(decide(base).kind, 'Speculate')
  for (const [field, value, reason] of [
    ['isRootWork', false, 'not-root-work'],
    ['requestKind', 'strength-replica', 'not-work-main'],
    ['requestKind', 'interaction-repair', 'not-work-main'],
    ['canonicalRole', 'manager', 'role-ineligible'],
    ['hasPrefixProbe', true, 'prefix-probe'],
    ['isAttachedOrInternalLeaf', true, 'attached-or-internal-leaf'],
    ['ownerCancelled', true, 'owner-cancelled'],
    ['targetProviderRunBound', false, 'target-provider-run-unbound'],
    ['eventStoreHealthy', false, 'event-store-unhealthy'],
    ['hostCanaryHealthy', false, 'host-canary-unhealthy'],
    ['predictorAvailable', false, 'predictor-unavailable'],
    ['costModelAvailable', false, 'cost-model-unavailable'],
  ]) {
    const decision = decide({ ...base, [field]: value })
    assert.equal(decision.kind, 'Skip', field)
    assert.equal(decision.budget, 'K0', field)
    assert.equal(decision.reason, reason, field)
  }
})

test('WHAT[speculative-investigation-002] invalid role and request labels are rejected by the test boundary decoder', () => {
  for (const field of ['canonicalRole', 'requestKind']) {
    const result = decide({ ...base, [field]: 'unknown' })
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
})

test('WHAT[speculative-investigation-002] current policy admits Engineer despite the retained historical-role whitelist', { todo: 'GAP-184 / 48-D1: reconcile the policy with current identity catalog without silently expanding the contract' }, () => {
  assert.equal(decide({ ...base, canonicalRole: 'engineer', selectedAgent: 'engineer' }).budget, 'K0')
})

test.todo('WHAT[speculative-investigation-002] GAP-183: actual Work transform must derive every prerequisite from frozen authority and exact target evidence; the policy matrix alone does not prove this wiring')
