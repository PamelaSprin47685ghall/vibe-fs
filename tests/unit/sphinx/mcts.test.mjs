import assert from 'node:assert/strict'
import test from 'node:test'
import {
  backupMctsValue,
  degenerateMctsSelection,
  selectMctsNode,
  syncMcts,
  createEpistemicState,
  closure,
} from '../../../src/sphinx/kernel/index.js'

test('mcts_uct_prefers_higher_reward_after_rollouts', () => {
  const selected = degenerateMctsSelection(
    [
      { id: 'weak', semanticKey: 'weak' },
      { id: 'strong', semanticKey: 'strong' },
    ],
    { weak: 0.1, strong: 0.95 },
    24,
  )
  assert.equal(selected.semanticKey, 'strong')
})

test('backup_mcts_value_accumulates_visits', () => {
  let state = createEpistemicState('mcts?')
  state = backupMctsValue(state, 'node:a', 0.8)
  state = backupMctsValue(state, 'node:a', 0.6)
  assert.equal(state.mcts.nodes['node:a'].visits, 2)
  assert.ok(state.mcts.nodes['node:a'].valueSum > 1.3)
})

test('sync_mcts_attaches_transposition_nodes_in_closure', () => {
  let state = createEpistemicState('rollout?')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Why: 1 },
      facets: { explanatory: 1 },
    },
    { exogenous: true },
  )
  state = closure(
    state,
    {
      type: 'Candidates',
      items: [{ method: 'Abduction', text: 'x', semanticKey: 'abd:x' }],
    },
    { exogenous: true },
  )
  assert.ok(state.mcts.transpositions >= 1)
  assert.ok(selectMctsNode(state))
})
