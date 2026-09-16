import assert from 'node:assert/strict'
import test from 'node:test'

import { mapOfEntries, run, uct } from './support.mjs'

const map = (entries) => mapOfEntries(entries)
const model = (root, children, terminalReward, prior) => ({ root, children, terminalReward, prior })

test('WHAT[EPI-010] mcts_selection_expansion_rollout_backup_prefers_high_value_branch', () => {
  const result = run(
    40,
    model(
      'root',
      map([
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
    ),
  )

  assert.equal(result.bestAction, 'strong')
  assert.equal(result.iterations, 40)
})

test('WHAT[EPI-010] graph_mcts_shares_transposition_statistics_by_semantic_node_key', () => {
  const result = run(
    20,
    model(
      'root',
      map([
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
    ),
  )

  const shared = result.nodes.find((node) => node.semanticKey === 'shared')
  assert.ok(shared.visits > 1)
  assert.ok(result.nodes.length <= 4)
})

test('WHAT[EPI-010] uct_for_unvisited_node_is_infinite', () => {
  const node = { semanticKey: 'new', visits: 0, valueSum: 0, prior: 0.5 }
  assert.equal(uct(10, Math.SQRT2, node), Number.POSITIVE_INFINITY)
})
