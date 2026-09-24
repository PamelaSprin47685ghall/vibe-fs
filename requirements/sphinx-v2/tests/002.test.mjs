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

// WHAT[sphinx-v2-002]: the default profile's constants are engineering limits, not
// semantic weights. Their config hash input must contain nothing that ranks a plan.

test('WHAT[sphinx-v2-002] the profile config hash input carries no ranking terms', () => {
  const input = Loop.profileConfigInput()

  // None of these may appear: they would make an engineering constant look like a
  // judgement about a plan, a probe or an answer.
  for (const token of ['qualityWeight', 'methodUtility', 'expectedRootGain', 'answerQuality', 'gain']) {
    assert.equal(input.includes(token), false, `config input must not contain ${token}`)
  }

  // It does carry the declared engineering constants, so a change to any of them is
  // visible as a config change rather than invisible.
  for (const token of ['sphinx.default@2', '1', '8', '100']) {
    assert.equal(input.includes(token), true, `config input must carry ${token}`)
  }
})

// WHAT[sphinx-v2-002]: a negative engineering constant is refused rather than silently
// producing a profile that can never do any work.

test('WHAT[sphinx-v2-002] an invalid profile is refused', () => {
  const bad = Loop.profileWith({ MaxActivePlanCards: 0 })
  assert.equal(Core.isError(Loop.profileValidate(bad)), true)

  const negative = Loop.profileWith({ FitMaxIterations: -1 })
  assert.equal(Core.isError(Loop.profileValidate(negative)), true)

  const brokenTolerance = Loop.profileWith({ FitGradientTolerance: 0 })
  assert.equal(Core.isError(Loop.profileValidate(brokenTolerance)), true)
})

test('WHAT[sphinx-v2-002] the declared default profile is admissible', () => {
  assert.equal(Core.isOk(Loop.profileValidate(Loop.profileDefault())), true)
})

// WHAT[sphinx-v2-002]: delegated execution makes no independence claim.

test('WHAT[sphinx-v2-002] delegated execution claims no independence', () => {
  assert.equal(Loop.profileClaimsIndependence(Loop.profileDefault()), false)

  const independent = Loop.profileWith({ ExecutionMode: 1 })
  assert.equal(Loop.profileClaimsIndependence(independent), true)
})
