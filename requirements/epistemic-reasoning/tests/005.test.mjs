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

test('WHAT[epistemic-reasoning-005] semantic_assessment_and_candidates_are_control_observations_not_world_evidence', () => {
  const store = createStore()
  const started = start(store, '为什么天空是蓝色？')
  assessWhy(store, started.handle)

  let current = state(store, started.handle)
  assert.equal(current.evidence.length, 0)
  assert.equal(current.findings.length, 0)

  const next = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '瑞利散射是否足以解释蓝色？',
        semanticKey: 'question:rayleigh',
        expectedRootGain: 0.95,
        cost: 0.2,
      },
    ],
  })
  assert.equal(next.request.type, 'InvestigateRequest')

  current = state(store, started.handle)
  assert.equal(current.evidence.length, 0)
  assert.equal(current.findings.length, 0)
})
test('WHAT[epistemic-reasoning-005] candidate_question_must_be_investigated_before_it_can_affect_answer', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)

  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'CausalMechanism',
        question: '花青素合成链如何产生红色？',
        semanticKey: 'question:anthocyanin-chain',
        expectedRootGain: 0.9,
        gatewayGain: 0.2,
        cost: 0.2,
      },
    ],
  })

  assert.equal(candidate.status, 'yield')
  assert.equal(candidate.request.type, 'InvestigateRequest')
  assert.equal(candidate.request.action.semanticKey, 'question:anthocyanin-chain')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createStore, start, resume, state, assessWhy } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-005] synthesis_is_information_propagation_not_information_acquisition', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)
  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'CausalMechanism',
        question: '调查机制',
        semanticKey: 'question:mechanism',
        expectedRootGain: 0.95,
        cost: 0.1,
      },
    ],
  })
  const investigated = resume(store, started.handle, {
    type: 'Investigation',
    actionKey: candidate.request.action.id,
    findings: [
      {
        semanticKey: 'finding:mechanism',
        text: '已有证据支持机制。',
        evidenceKeys: ['evidence:mechanism'],
      },
    ],
    evidence: [
      {
        semanticKey: 'evidence:mechanism',
        proposition: '外生观测。',
        source: { id: 'tool-result', kind: 'tool' },
        dependencyKey: 'tool-result',
      },
    ],
  })
  assert.equal(investigated.request.type, 'GenerateCandidatesRequest')
  const regenerated = resume(store, started.handle, { type: 'Candidates', items: [] })
  assert.equal(regenerated.request.type, 'SynthesizeRequest')
  const before = state(store, started.handle).evidence.length

  resume(store, started.handle, {
    type: 'Synthesis',
    text: '把已有发现组织为解释。',
    findingKeys: ['finding:mechanism'],
  })
  assert.equal(state(store, started.handle).evidence.length, before)
})
}
