import assert from 'node:assert/strict'
import test from 'node:test'

import { mapOfEntries, toList } from '../support/domain.mjs'
import { Model, run, uct } from '../../../dist/Sphinx/MonteCarlo.js'
import { MonteCarloNode } from '../../../dist/Sphinx/RuntimeTypes.js'

const map = (entries) => mapOfEntries(entries)
const childMap = (entries) => map(entries.map(([key, children]) => [key, toList(children)]))

test('mcts_selection_expansion_rollout_backup_prefers_high_value_branch', () => {
  const model = new Model(
    'root',
    childMap([
      ['root', ['weak', 'strong']],
      ['weak', ['weak-terminal']],
      ['strong', ['strong-terminal']],
    ]),
    map([
      ['weak-terminal', 0.1],
      ['strong-terminal', 0.95],
    ]),
    map([
      ['weak', 0.5],
      ['strong', 0.5],
    ]),
  )

  const result = run(40, model)
  assert.equal(result.BestAction, 'strong')
  assert.equal(result.Iterations, 40)
})

test('graph_mcts_shares_transposition_statistics_by_semantic_node_key', () => {
  const model = new Model(
    'root',
    childMap([
      ['root', ['a', 'b']],
      ['a', ['shared']],
      ['b', ['shared']],
    ]),
    map([['shared', 0.8]]),
    map([
      ['a', 0.5],
      ['b', 0.5],
      ['shared', 0.8],
    ]),
  )

  const result = run(20, model)
  const shared = result.Nodes.get('shared')
  assert.ok(shared.Visits > 1)
  assert.ok(result.Nodes.size <= 4)
})

test('uct_for_unvisited_node_is_infinite', () => {
  const node = new MonteCarloNode('new', 0, 0, 0.5)
  assert.equal(uct(10, Math.SQRT2, node), Number.POSITIVE_INFINITY)
})
