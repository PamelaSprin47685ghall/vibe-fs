import assert from 'node:assert/strict'
import test from 'node:test'

import * as Ordinal from '../../../dist/Sphinx/V2/Plugins/Ordinal/Surface.js'
import * as Bayes from '../../../dist/Sphinx/V2/Plugins/Bayes/Surface.js'
import * as AStar from '../../../dist/Sphinx/V2/Plugins/AStar/Surface.js'
import * as Mcts from '../../../dist/Sphinx/V2/Plugins/Mcts/Surface.js'

// WHAT[sphinx-v2-025]: the pairwise likelihood is computed in log space, so an extreme
// comparison stays finite instead of becoming log(0).

test('WHAT[sphinx-v2-025] logSigmoid stays finite for extreme comparisons', () => {
  assert.equal(Number.isFinite(Ordinal.logSigmoid(1000)), true)
  assert.equal(Number.isFinite(Ordinal.logSigmoid(-1000)), true)
  assert.equal(Ordinal.logSigmoid(0), -Math.LN2)
})

test('WHAT[sphinx-v2-025] log-sum-exp does not overflow on a large pair', () => {
  const value = Ordinal.logSumExp(1000, 1000)
  assert.equal(Number.isFinite(value), true)
  assert.ok(value > 1000)
})

// WHAT[sphinx-v2-014]: a ranked ballot expands into composite pairs whose cluster the
// caller must carry; the pair count is not an independent sample size.

test('WHAT[sphinx-v2-014] a ranking expands to composite pairs, not independent votes', () => {
  const pairs = Ordinal.compositePairs(Ordinal.listOfItems(['a', 'b', 'c']))
  assert.equal(Ordinal.listCount(Ordinal.listOfItems(['a', 'b', 'c'])), 3)
  assert.equal(pairs.head[0], 'a')
  assert.equal(pairs.head[1], 'b')
})

test('WHAT[sphinx-v2-014] best and worst must differ', () => {
  assert.equal(Ordinal.isError(Ordinal.decodeMaxDiff({ best: 'a', worst: 'a' })), true)
})

// WHAT[sphinx-v2-022]: an abstention is not a vote and not a missing datum.

test('WHAT[sphinx-v2-022] abstain, tie and conditional are separate channels', () => {
  assert.equal(Ordinal.isDirectional(Ordinal.judgmentOf('abstain')), false)
  assert.equal(Ordinal.isAbstention(Ordinal.judgmentOf('abstain')), true)

  assert.equal(Ordinal.isTie(Ordinal.judgmentOf('tie')), true)

  assert.equal(Ordinal.isConditional(Ordinal.judgmentOf('conditional')), true)
  assert.equal(Ordinal.isDirectional(Ordinal.judgmentOf('conditional')), false)
})

// WHAT[sphinx-v2-023]: labels the ticket never showed are refused.

test('WHAT[sphinx-v2-023] a label outside the presented set is refused', () => {
  const response = {
    Judgment: null,
    Rationale: '',
    SourceLabels: ['item_1'],
    ProposedAlternatives: [],
  }

  assert.equal(
    Ordinal.isError(Ordinal.labelsWithin(Ordinal.stringSetOf(['item_1']), response, Ordinal.stringSetOf(['item_2']))),
    true,
  )
})

// WHAT[sphinx-v2-004]: the Bayes posterior is computed over declared factors and
// normalizes to a hand-checkable result.

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

test('WHAT[sphinx-v2-026] a zero prior stays zero', () => {
  const hypotheses = Bayes.listOfItems([{ Key: 'h1', Prior: 0.0 }, { Key: 'h2', Prior: 1.0 }])
  const posterior = Bayes.okPosterior(Bayes.priorOnly(hypotheses))

  assert.equal(Bayes.probabilityOf(posterior, 'h1'), 0.0)
  assert.equal(Bayes.probabilityOf(posterior, 'h2'), 1.0)
})

// WHAT[sphinx-v2-026]: A* reopens a closed node when a cheaper path arrives, and a
// negative edge is refused rather than silently accepted.

test('WHAT[sphinx-v2-026] A* reopens a closed node and returns the true cost', () => {
  const problem = {
    Start: 'S',
    Goal: 'G',
    Edges: AStar.listOfItems([
      { FromNode: 'S', ToNode: 'A', Cost: 3 },
      { FromNode: 'S', ToNode: 'B', Cost: 1 },
      { FromNode: 'B', ToNode: 'A', Cost: 1 },
      { FromNode: 'A', ToNode: 'G', Cost: 2 },
      { FromNode: 'B', ToNode: 'G', Cost: 100 },
    ]),
    Heuristic: AStar.stringFloatMapOf([['S', 4], ['A', 0], ['B', 3], ['G', 0]]),
    HeuristicAdmissible: true,
  }

  const solved = AStar.okValue(AStar.solve(problem))
  assert.equal(AStar.costOf(solved), 4)
})

test('WHAT[sphinx-v2-026] a negative edge is refused', () => {
  const problem = {
    Start: 'S',
    Goal: 'G',
    Edges: AStar.listOfItems([{ FromNode: 'S', ToNode: 'A', Cost: -1 }]),
    Heuristic: AStar.stringFloatMapOf([]),
    HeuristicAdmissible: true,
  }

  assert.equal(AStar.isError(AStar.solve(problem)), true)
})

// WHAT[sphinx-v2-013]: an unvisited node is handled before any mean comparison, and
// the state key carries the horizon so incompatible statistics never merge.

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
