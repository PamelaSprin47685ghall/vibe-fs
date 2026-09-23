import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import * as Persist from '../../../dist/Sphinx/V2/Persistence/Surface.js'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

// Shared fixtures. Every record is plain JS: the surface reads PascalCase fields.
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

// A decision estimate. `Rank` is the only thing the selector reads, so a plan that was
// never compared has no rank at all rather than a rank of zero.
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

test('WHAT[sphinx-v2-013] a better comparison changes the selected plan', () => {
  const scope = 'scope_1'

  const before = Loop.decisionSelect(scope, [modelEstimate('check-A', scope, 0.5, 1), modelEstimate('find-new', scope, 0.2, 2)])
  assert.equal(before.SelectedPlanId, 'check-A')

  // The witness now prefers find-new. The next dispatch must change with it.
  const after = Loop.decisionSelect(scope, [modelEstimate('check-A', scope, 0.2, 2), modelEstimate('find-new', scope, 0.7, 1)])
  assert.equal(after.SelectedPlanId, 'find-new')

  // ID order must not decide this: the ids are in opposite order in both inputs.
  assert.notEqual(before.SelectedPlanId, after.SelectedPlanId)
})

test('WHAT[sphinx-v2-013] selection never reads ID order', () => {
  const scope = 'scope_1'
  const forward = Loop.decisionSelect(scope, [modelEstimate('aaa', scope, 0.9, 1), modelEstimate('zzz', scope, 0.1, 2)])
  const reversed = Loop.decisionSelect(scope, [modelEstimate('zzz', scope, 0.1, 2), modelEstimate('aaa', scope, 0.9, 1)])

  assert.equal(forward.SelectedPlanId, reversed.SelectedPlanId)
})

// ---------------------------------------------------------------------------
// 4. answer.now competes on the same footing as any investigation.
// ---------------------------------------------------------------------------

test('WHAT[sphinx-v2-013] an infeasible plan is excluded with an operational reason', () => {
  const scope = 'scope_1'
  const exclusion = Loop.decisionExclude('probe.minimal-discriminator', 'permission-required')

  assert.equal(exclusion.PlanId, 'probe.minimal-discriminator')
  assert.equal(exclusion.Reason, 'permission-required')
})

// ---------------------------------------------------------------------------
// 6. a status read is free, and the state machine classifies without spending.
// ---------------------------------------------------------------------------
