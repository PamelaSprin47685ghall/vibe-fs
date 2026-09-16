import assert from 'node:assert/strict'
import test from 'node:test'

import { library, phase0Names, createStore, start, resume } from './support.mjs'

test('WHAT[EPI-007] method_library_preserves_phase0_kernel_and_extends_without_pipeline_semantics', () => {
  const names = [...library]
  assert.deepEqual([...phase0Names].sort(), [
    'Abduction',
    'Analogy',
    'Counterexample',
    'Multidisciplinary',
    'Synthesis',
  ])
  assert.ok(names.includes('CausalMechanism'))
  assert.ok(names.includes('BaseRate'))
  assert.ok(names.includes('Falsification'))
  assert.ok(names.includes('SourceTriangulation'))
  assert.ok(names.includes('OntologyRepair'))
})

test('WHAT[EPI-007] why_question_activates_multiple_generators_from_distribution_and_facets', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  const result = resume(store, started.handle, {
    type: 'SemanticAssessment',
    forms: { Why: 0.7, How: 0.3 },
    facets: { causal: 0.9, explanatory: 1, 'multi-domain': 0.8 },
  })

  assert.equal(result.request.type, 'GenerateCandidatesRequest')
  assert.ok(result.request.methods.includes('Multidisciplinary'))
  assert.ok(result.request.methods.includes('Abduction'))
  assert.ok(result.request.methods.includes('CausalMechanism'))
  assert.equal(result.request.methods.includes('Synthesis'), false)
})

test('WHAT[EPI-007] predictive_polar_question_activates_base_rate_and_falsification', () => {
  const store = createStore()
  const started = start(store, '明天白银会涨吗？')
  const result = resume(store, started.handle, {
    type: 'SemanticAssessment',
    forms: { Polar: 0.9, Other: 0.1 },
    facets: { predictive: 1, falsification: 0.8 },
  })

  assert.ok(result.request.methods.includes('BaseRate'))
  assert.ok(result.request.methods.includes('Falsification'))
  assert.ok(result.request.methods.includes('Counterexample'))
})
