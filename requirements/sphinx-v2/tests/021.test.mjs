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

test('WHAT[sphinx-v2-021] an estimate kind is preserved through ranking', () => {
  const scope = 'scope_1'

  const ranked = Loop.decisionRank(scope, [
    modelEstimate('answer.now', scope, 0.4, 3),
    ordinalOnly('probe.gap-and-stop', scope, 1),
    unestimated('find-new', scope),
  ])

  // Only the compared plans are placed; the unestimated one is absent rather than
  // being handed a rank of zero.
  assert.equal(ranked.length, 2)
  assert.equal(ranked[0].PlanId, 'probe.gap-and-stop')
  assert.equal(ranked[0].Rank, 1)
  assert.equal(ranked[1].PlanId, 'answer.now')
})

test('WHAT[sphinx-v2-021] a provisional order is never promoted to a model estimate', () => {
  const scope = 'scope_1'
  const numeric = Loop.decisionSupportsNumeric(scope, [provisional('answer.now', scope, 1)])

  assert.equal(numeric, false, 'a single-response provisional order is not a numeric estimate')
})

// ---------------------------------------------------------------------------
// 3. selection: the highest estimate wins, and the winner changes when the
//    comparison changes. This is the property the whole design exists for.
// ---------------------------------------------------------------------------
