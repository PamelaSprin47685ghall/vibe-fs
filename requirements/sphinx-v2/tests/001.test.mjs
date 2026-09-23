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

test('WHAT[sphinx-v2-001] an inquiry keeps the original goal and its revision', () => {
  const goal = goalSpec('给出一个更稳妥的发布流程')
  assert.equal(Core.goalTextOf(goal), '给出一个更稳妥的发布流程')
  assert.equal(Number(Core.goalRevisionValue(goal)), 0)
})

test('WHAT[sphinx-v2-001] an amendment advances the revision and preserves the original text', () => {
  const before = goalSpec('给出一个更稳妥的发布流程')
  const amended = ok(Core.goalTryAmend('user', Core.listOfItems(['不影响线上用户']), '给出不影响线上用户的发布流程', before))

  assert.equal(Core.goalRevisionValue(amended), 1n)
  assert.equal(Core.goalTextOf(amended), '给出不影响线上用户的发布流程')
  // The amendment list is durable, so the change is auditable rather than overwritten.
  assert.equal(Core.goalAmendmentsOf(amended).length, 1)
})

// ---------------------------------------------------------------------------
// 2. the comparison the LLM actually returns is ordinal, and it stays ordinal.
// ---------------------------------------------------------------------------
