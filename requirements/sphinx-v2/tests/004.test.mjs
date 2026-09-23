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

test('WHAT[sphinx-v2-004] a two-hypothesis factor set normalizes by hand', () => {
  const hypotheses = Bayes.listOfItems([{ Key: 'h1', Prior: 0.5 }, { Key: 'h2', Prior: 0.5 }])

  const factors = Bayes.listOfItems([
    {
      ObservationId: 'o1',
      DependencyKey: 'dep_1',
      Likelihoods: Bayes.stringFloatMapOf([['h1', 0.8], ['h2', 0.4]]),
      Qualified: true,
    },
  ])

  const posterior = Bayes.okPosterior(Bayes.infer(hypotheses, factors))

  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h1') - 2 / 3) < 1e-12)
  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h2') - 1 / 3) < 1e-12)
})

test('WHAT[sphinx-v2-004] a repeated delivery of one observation is counted once', () => {
  const hypotheses = Bayes.listOfItems([{ Key: 'h1', Prior: 0.5 }, { Key: 'h2', Prior: 0.5 }])

  const twice = Bayes.listOfItems([
    {
      ObservationId: 'o1',
      DependencyKey: 'dep_1',
      Likelihoods: Bayes.stringFloatMapOf([['h1', 0.8], ['h2', 0.4]]),
      Qualified: true,
    },
    {
      ObservationId: 'o1',
      DependencyKey: 'dep_1',
      Likelihoods: Bayes.stringFloatMapOf([['h1', 0.8], ['h2', 0.4]]),
      Qualified: true,
    },
  ])

  const posterior = Bayes.okPosterior(Bayes.infer(hypotheses, twice))
  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h1') - 2 / 3) < 1e-12)
})
