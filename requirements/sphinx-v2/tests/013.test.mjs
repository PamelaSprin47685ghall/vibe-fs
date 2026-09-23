import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import * as Persist from '../../../dist/Sphinx/V2/Persistence/Surface.js'
import * as Ordinal from '../../../dist/Sphinx/V2/Plugins/Ordinal/Surface.js'
import * as Bayes from '../../../dist/Sphinx/V2/Plugins/Bayes/Surface.js'
import * as AStar from '../../../dist/Sphinx/V2/Plugins/AStar/Surface.js'
import * as Mcts from '../../../dist/Sphinx/V2/Plugins/Mcts/Surface.js'

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

test('WHAT[sphinx-v2-013] an unvisited node sorts before any mean comparison', () => {
  const node = Mcts.nodeStats(0, 0, 0, 'model', 1)
  assert.equal(Mcts.uctOf(4, 1.0, 0.0, 1.0, node), Number.POSITIVE_INFINITY)
})

test('WHAT[sphinx-v2-013] a visited node reports its sample mean', () => {
  const node = Mcts.nodeStats(4, 2.0, 1.0, 'model', 1)
  assert.equal(Mcts.meanOf(node), 0.5)
})

test('WHAT[sphinx-v2-013] the state key carries model and horizon', () => {
  const one = Mcts.stateKeyOf('model_a', Mcts.listOfItems([]), 1)
  const two = Mcts.stateKeyOf('model_a', Mcts.listOfItems([]), 2)

  assert.notEqual(one, two)
})
