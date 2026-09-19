import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync, readdirSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { close, createStore, start, resume, state, assessWhy, relativeServerEntry } = await import("./support.mjs");

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '../../..')

test('WHAT[epistemic-reasoning-007] contract_keeps_distribution_after_semantic_assessment', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')

  const assessed = assessWhy(store, started.handle)
  assert.equal(assessed.status, 'yield')
  assert.equal(assessed.request.type, 'GenerateCandidatesRequest')
  assert.equal(assessed.request.contract.formBelief.Why, 0.8)
  assert.equal(assessed.request.contract.formBelief.How, 0.2)
  assert.equal(assessed.request.contract.contractBelief.Explanation, 0.8)
  assert.equal(assessed.request.contract.contractBelief.Plan, 0.2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { library, phase0Names, createStore, start, resume } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-007] method_library_preserves_phase0_kernel_and_extends_without_pipeline_semantics', () => {
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
test('WHAT[epistemic-reasoning-007] why_question_activates_multiple_generators_from_distribution_and_facets', () => {
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
test('WHAT[epistemic-reasoning-007] predictive_polar_question_activates_base_rate_and_falsification', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createStore, start, resume, state, assessWhy } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-007] later_semantic_assessment_updates_control_belief_without_creating_evidence', () => {
  const store = createStore()
  const started = start(store, '为什么程序卡住？')
  assessWhy(store, started.handle)
  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '继续调查后，用户真正需要的是解释还是修复方案？',
        semanticKey: 'question:intent-shift',
        expectedRootGain: 0.8,
        cost: 0.1,
      },
    ],
  })

  const next = resume(store, started.handle, {
    type: 'Investigation',
    actionKey: candidate.request.action.id,
    semanticAssessment: {
      forms: { Why: 0.3, How: 0.7 },
      facets: { causal: 0.4, explanatory: 0.3, diagnostic: 0.9 },
      intents: ['repair'],
    },
  })

  const current = state(store, started.handle)
  assert.equal(next.request.type, 'GenerateCandidatesRequest')
  assert.ok(Math.abs(next.request.contract.formBelief.How - 0.7) < 1e-12)
  assert.equal(current.evidence.length, 0)
  assert.equal(current.findings.length, 0)
})
}
