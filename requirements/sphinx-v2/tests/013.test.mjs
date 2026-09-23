import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import * as Persist from '../../../dist/Sphinx/V2/Persistence/Surface.js'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

const goalSpec = (text) =>
  Core.goalCreate({
    GoalId: Core.goalIdCreate('goal_loop'),
    Revision: ok(Core.revisionTryCreate(0n)),
    OriginalText: text,
    Constraints: Core.setOf([]),
    MaterialRefs: Core.setOf([]),
    AuthorizationRef: 'user-auth-1',
    CreatedBy: 'user',
    Amendments: Core.listOfItems([]),
  })

const modelEstimate = (planId, scopeId, location, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: location,
  Rank: rank,
  Kind: 'model-estimate',
})

const ordinalOnly = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'ordinal-only',
})

const provisional = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'single-response-provisional',
})

const unestimated = (planId, scopeId) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: null,
  Kind: 'unestimated',
})

const resourceSpec = (name, unit, limit) => ({
  Name: name,
  Kind: Core.resourceKindCreate('consumed', unit),
  AuthorizedLimit: limit,
})

// WHAT[sphinx-v2-013]: a provider that reports no usage keeps its whole reservation.
// Writing zeros would let the ledger claim an expensive call was free.

test('WHAT[sphinx-v2-013] a run without usage stays unresolved', () => {
  const outcome = Loop.providerOutcome('', 0, 0, 0, true)
  assert.equal(Loop.providerUsageUnresolved(outcome), true)
})

test('WHAT[sphinx-v2-013] a run with usage is resolved', () => {
  const outcome = Loop.providerOutcome('', 100, 200, 1, false)
  assert.equal(Loop.providerUsageUnresolved(outcome), false)
})

test('WHAT[sphinx-v2-013] intake, output and call counts are preserved', () => {
  const outcome = Loop.providerOutcome('', 100, 200, 3, false)
  const counts = Loop.providerUsageCounts(outcome)

  assert.equal(counts[0], 100)
  assert.equal(counts[1], 200)
  assert.equal(counts[2], 3)
})

// WHAT[sphinx-v2-013]: the point of the whole design. When the comparison changes, the
// selection changes with it. ID order must never decide it.

test('WHAT[sphinx-v2-013] a better comparison changes the selected plan', () => {
  const scope = 'scope_1'

  const before = Loop.decisionSelect(scope, [
    modelEstimate('check-A', scope, 0.5, 1),
    modelEstimate('find-new', scope, 0.2, 2),
  ])

  assert.equal(before.SelectedPlanId, 'check-A')

  const after = Loop.decisionSelect(scope, [
    modelEstimate('check-A', scope, 0.2, 2),
    modelEstimate('find-new', scope, 0.7, 1),
  ])

  assert.equal(after.SelectedPlanId, 'find-new')

  // The ids are in opposite order in both inputs, so this cannot be an id sort.
  assert.notEqual(before.SelectedPlanId, after.SelectedPlanId)
})

test('WHAT[sphinx-v2-013] selection never reads ID order', () => {
  const scope = 'scope_1'
  const forward = Loop.decisionSelect(scope, [
    modelEstimate('aaa', scope, 0.9, 1),
    modelEstimate('zzz', scope, 0.1, 2),
  ])

  const reversed = Loop.decisionSelect(scope, [
    modelEstimate('zzz', scope, 0.1, 2),
    modelEstimate('aaa', scope, 0.9, 1),
  ])

  assert.equal(forward.SelectedPlanId, reversed.SelectedPlanId)
})
