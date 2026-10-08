import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

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

test('WHAT[sphinx-v2-029] answer.now wins when it is estimated highest', () => {
  const scope = 'scope_1'
  const chosen = Loop.decisionSelect(scope, [
    modelEstimate('answer.now', scope, 0.8, 1),
    modelEstimate('probe.counterexample', scope, 0.3, 2),
  ])

  assert.equal(chosen.SelectedPlanId, 'answer.now')
})

test('WHAT[sphinx-v2-029] an investigation wins when it is estimated highest', () => {
  const scope = 'scope_1'
  const chosen = Loop.decisionSelect(scope, [
    modelEstimate('answer.now', scope, 0.3, 2),
    modelEstimate('probe.counterexample', scope, 0.8, 1),
  ])

  assert.equal(chosen.SelectedPlanId, 'probe.counterexample')
})

const estimateScope = 'scope-answer-now-unestimated'
const declaredEstimate = (PlanId, Kind, Rank, Location = null) => ({
  PlanId, ScopeId: estimateScope, Kind, Rank, Location,
})

test('WHAT[sphinx-v2-029] answer.now receives no default advantage when it is Unestimated', () => {
  for (const Kind of ['ordinal-only', 'single-response-provisional', 'model-estimate']) {
    const investigation = declaredEstimate('investigation', Kind, 2, Kind === 'model-estimate' ? 0.4 : null)
    const answerNow = declaredEstimate('answer.now', 'unestimated', 0, 1)
    for (const candidates of [[answerNow, investigation], [investigation, answerNow]]) {
      assert.equal(Loop.decisionSelect(estimateScope, candidates).SelectedPlanId, investigation.PlanId)
    }
  }
})

test('WHAT[sphinx-v2-029] an estimated answer.now may win against an Unestimated investigation', () => {
  const answerNow = declaredEstimate('answer.now', 'ordinal-only', 2)
  const investigation = declaredEstimate('investigation', 'unestimated', 0, 1)
  for (const candidates of [[answerNow, investigation], [investigation, answerNow]]) {
    assert.equal(Loop.decisionSelect(estimateScope, candidates).SelectedPlanId, answerNow.PlanId)
  }
})

test('WHAT[sphinx-v2-029] selected ordinal and provisional answers retain their actual estimate kind', () => {
  for (const Kind of ['ordinal-only', 'single-response-provisional', 'model-estimate']) {
    const winner = declaredEstimate('answer.now', Kind, 1, Kind === 'model-estimate' ? 0.8 : null)
    const alternative = declaredEstimate('investigation', Kind, 2, Kind === 'model-estimate' ? 0.2 : null)
    const selected = Loop.decisionSelect(estimateScope, [winner, alternative])
    assert.equal(selected.SelectedPlanId, winner.PlanId)
    assert.equal(selected.SelectedKind, Kind)
    assert.equal(Loop.decisionRank(estimateScope, [winner, alternative])[0].Kind, Kind)
  }
})

// ---------------------------------------------------------------------------
// 5. exclusion is operational, never a semantic demotion.
// ---------------------------------------------------------------------------
