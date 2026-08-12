import assert from 'node:assert/strict'
import test from 'node:test'
import {
  contractRepresentation,
  optimizeRepresentation,
  paretoRepresentative,
  createEpistemicState,
  closure,
} from '../../../src/sphinx/kernel/index.js'

test('pareto_representative_keeps_non_dominated_action', () => {
  const rep = paretoRepresentative([
    { id: 'a', semanticKey: 'eq:1', value: 0.4, cost: 2 },
    { id: 'b', semanticKey: 'eq:2', value: 0.8, cost: 1 },
  ])
  assert.equal(rep.id, 'b')
})

test('contract_representation_merges_equivalence_class', () => {
  const { compressed, classes } = contractRepresentation([
    { id: 'a', semanticKey: 'same', equivalenceClass: 'class:1', value: 0.3, cost: 1 },
    { id: 'b', semanticKey: 'same-2', equivalenceClass: 'class:1', value: 0.9, cost: 1 },
  ])
  assert.equal(compressed.length, 1)
  assert.equal(classes['class:1'].length, 2)
  assert.equal(compressed[0].id, 'b')
})

test('optimize_representation_runs_in_closure', () => {
  let state = createEpistemicState('represent?')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Why: 1 },
      facets: { explanatory: 1 },
    },
    { exogenous: true },
  )
  assert.ok(state.represent)
  assert.ok(Array.isArray(state.represent.pivots))
})
