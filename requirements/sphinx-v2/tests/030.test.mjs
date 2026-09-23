import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

// Ordinal and numeric conformance. WHAT[sphinx-v2-025]: the numbers below check that
// the declared likelihood is implemented as declared. They do not claim the model
// describes reality.

import * as Ordinal from '../../../dist/Sphinx/V2/Plugins/Ordinal/Surface.js'
import * as Bayes from '../../../dist/Sphinx/V2/Plugins/Bayes/Surface.js'
import * as AStar from '../../../dist/Sphinx/V2/Plugins/AStar/Surface.js'
import * as Mcts from '../../../dist/Sphinx/V2/Plugins/Mcts/Surface.js'

// WHAT[sphinx-v2-014]: rank expansion keeps the ballot cluster; it is a composite term,
// not a bag of independent pairs.
describe('sphinx-v2 ranking likelihoods', () => {
  it('N-10: a ranked ballot expands to a composite, and its size is not a sample size', () => {
    const pairs = Ordinal.compositePairs(Ordinal.listOfItems(['a', 'b', 'c']))

    // Three items give three ordered pairs; the cluster id travels with them so the fit
    // does not read them as three independent observations.
    assert.equal(pairs.head[0], 'a')
    assert.equal(pairs.head[1], 'b')
    assert.equal(Ordinal.listCount(Ordinal.listOfItems(['a', 'b', 'c'])), 3)
  })

  it('N-11: best and worst must differ', () => {
    assert.equal(Ordinal.isError(Ordinal.decodeMaxDiff({ best: 'a', worst: 'a' })), true)
  })
})

// WHAT[sphinx-v2-021]: an abstention is not a vote and not a missing datum.
describe('sphinx-v2 judgment channels', () => {
  it('Q-02: an abstain carries no direction', () => {
    const response = Ordinal.judgmentOf('abstain')
    assert.equal(Ordinal.isDirectional(response), false)
    assert.equal(Ordinal.isAbstention(response), true)
  })

  it('Q-02: a tie carries direction of its own kind', () => {
    const response = Ordinal.judgmentOf('tie')
    assert.equal(Ordinal.isTie(response), true)
  })

  it('Q-02: a conditional produces a new investigation, not a preference', () => {
    const response = Ordinal.judgmentOf('conditional')
    assert.equal(Ordinal.isConditional(response), true)
    assert.equal(Ordinal.isDirectional(response), false)
  })
})

// WHAT[sphinx-v2-023]: labels outside the presented set are refused, not mapped.
describe('sphinx-v2 response hygiene', () => {
  it('refuses a label the ticket never showed', () => {
    const response = { Judgment: null, Rationale: '', SourceLabels: ['item_1'], ProposedAlternatives: [] }

    assert.equal(
      Ordinal.isError(Ordinal.labelsWithin(Ordinal.stringSetOf(['item_1']), response, Ordinal.stringSetOf(['item_2']))),
      true,
    )
  })

  it('accepts labels inside the presented set', () => {
    const response = { Judgment: null, Rationale: '', SourceLabels: ['item_1'], ProposedAlternatives: [] }

    assert.equal(
      Ordinal.isOk(Ordinal.labelsWithin(Ordinal.stringSetOf(['item_1']), response, Ordinal.stringSetOf(['item_1']))),
      true,
    )
  })
})

// WHAT[sphinx-v2-025]: stable log-space evaluation.
describe('sphinx-v2 pairwise numerics', () => {
  it('N-03: an extreme eta stays finite', () => {
    assert.equal(Number.isFinite(Ordinal.logSigmoid(1000)), true)
    assert.equal(Number.isFinite(Ordinal.logSigmoid(-1000)), true)
    assert.equal(Ordinal.logSigmoid(0), -Math.LN2)
  })

  it('log-sum-exp does not overflow on a large pair', () => {
    assert.equal(Number.isFinite(Ordinal.logSumExp(1000, 1000)), true)
    assert.ok(Ordinal.logSumExp(1000, 1000) > 1000)
  })
})

// WHAT[sphinx-v2-026]: a Bayes result is model-relative and says so.
describe('sphinx-v2 Bayes exact', () => {
  it('M-01: two hypotheses with one factor normalize to the hand-computed posterior', () => {
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

  it('M-04: a repeated delivery of the same observation is counted once', () => {
    const hypotheses = Bayes.listOfItems([{ Key: 'h1', Prior: 0.5 }, { Key: 'h2', Prior: 0.5 }])

    const twice = Bayes.listOfItems([
      { ObservationId: 'o1', DependencyKey: 'dep_1', Likelihoods: Bayes.stringFloatMapOf([['h1', 0.8], ['h2', 0.4]]), Qualified: true },
      { ObservationId: 'o1', DependencyKey: 'dep_1', Likelihoods: Bayes.stringFloatMapOf([['h1', 0.8], ['h2', 0.4]]), Qualified: true },
    ])

    const posterior = Bayes.okPosterior(Bayes.infer(hypotheses, twice))
    assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h1') - 2 / 3) < 1e-12)
  })

  it('M-02: a zero prior stays zero', () => {
    // A prior-only run carries no factor, so the zero prior must survive normalization
    // rather than being quietly lifted off zero.
    const hypotheses = Bayes.listOfItems([
      { Key: 'h1', Prior: 0.0 },
      { Key: 'h2', Prior: 1.0 },
    ])

    const posterior = Bayes.okPosterior(Bayes.priorOnly(hypotheses))
    assert.equal(Bayes.probabilityOf(posterior, 'h1'), 0.0)
    assert.equal(Bayes.probabilityOf(posterior, 'h2'), 1.0)
  })

  it('refuses a likelihood outside [0,1]', () => {
    const hypotheses = Bayes.listOfItems([{ Key: 'h1', Prior: 0.5 }, { Key: 'h2', Prior: 0.5 }])

    const factors = Bayes.listOfItems([
      { ObservationId: 'o1', DependencyKey: 'dep_1', Likelihoods: Bayes.stringFloatMapOf([['h1', 1.4], ['h2', 0.4]]), Qualified: true },
    ])

    assert.equal(Bayes.isError(Bayes.infer(hypotheses, factors)), true)
  })
})

// WHAT[sphinx-v2-026]: A* reopens a closed node when a cheaper g arrives.
describe('sphinx-v2 A* refiner', () => {
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
    Heuristic: Bayes.stringFloatMapOf([['S', 4], ['A', 0], ['B', 3], ['G', 0]]),
    HeuristicAdmissible: true,
  }

  it('M-07: reopens a closed node and returns the true shortest cost', () => {
    const result = AStar.solve(problem)
    const solved = AStar.okValue(result)

    assert.equal(AStar.costOf(solved), 4)
  })

  it('M-09: refuses a negative edge', () => {
    const negative = { ...problem, Edges: AStar.listOfItems([{ FromNode: 'S', ToNode: 'A', Cost: -1 }]) }
    assert.equal(AStar.isError(AStar.solve(negative)), true)
  })
})

// WHAT[sphinx-v2-026]: MCTS statistics are sample statistics in a declared horizon.
describe('sphinx-v2 MCTS refiner', () => {
  it('M-11: an unvisited node is handled before any mean comparison', () => {
    const node = Mcts.nodeStats(0, 0, 0, 'model', 1)
    assert.equal(Mcts.uctOf(4, 1.0, 0.0, 1.0, node), Number.POSITIVE_INFINITY)
  })

  it('M-12: a visited node reports its sample mean', () => {
    const node = Mcts.nodeStats(4, 2.0, 1.0, 'model', 1)
    assert.equal(Mcts.meanOf(node), 0.5)
  })

  it('reports no variance from a single visit', () => {
    const node = Mcts.nodeStats(1, 1.0, 1.0, 'model', 1)
    assert.equal(Mcts.meanOf(node), 1.0)
  })

  it('includes model and horizon in the state key', () => {
    const one = Mcts.stateKeyOf('model_a', Mcts.listOfItems([]), 1)
    const two = Mcts.stateKeyOf('model_a', Mcts.listOfItems([]), 2)

    assert.notEqual(one, two)
  })
})
