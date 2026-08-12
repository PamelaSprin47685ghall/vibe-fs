import assert from 'node:assert/strict'
import test from 'node:test'
import {
  METHODS,
  EXTENDED_METHODS,
  allMethods,
  activateMethods,
  generateFromRules,
  createEpistemicState,
  deriveRootContract,
} from '../../../src/sphinx/kernel/index.js'

test('method_library_v1_stays_fixed', () => {
  assert.deepEqual(METHODS, [
    'Multidisciplinary',
    'Abduction',
    'Analogy',
    'Counterexample',
    'Synthesis',
  ])
})

test('extended_methodology_library_phase5', () => {
  assert.deepEqual(EXTENDED_METHODS, [
    'CausalMechanism',
    'BaseRate',
    'Dialectic',
    'Falsification',
    'BoundarySearch',
  ])
  assert.equal(allMethods().length, METHODS.length + EXTENDED_METHODS.length)
})

test('extended_methods_generate_candidates_for_polar_questions', () => {
  const blank = createEpistemicState('will silver rise tomorrow?')
  const contract = deriveRootContract({ Polar: 0.9, Other: 0.1 }, { predictive: 0.9 })
  const state = {
    ...blank,
    R: contract,
    B: { ...blank.B, formBelief: contract.formBelief, facets: contract.facets },
  }
  const activated = activateMethods(state, 0)
  assert.ok(activated.includes('BaseRate'))
  const generated = generateFromRules(state)
  assert.ok(generated.actions.some((action) => action.method === 'Falsification'))
})
