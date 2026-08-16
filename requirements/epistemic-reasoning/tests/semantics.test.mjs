import assert from 'node:assert/strict'
import test from 'node:test'

import { createStore, start, resume, state, assessWhy } from './support.mjs'

test('WHAT[EPI-003] ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)
  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '某个解释是否成立？',
        semanticKey: 'question:hypothesis',
        expectedRootGain: 0.9,
        cost: 0.1,
      },
    ],
  })

  const investigated = resume(store, started.handle, {
    type: 'Investigation',
    actionKey: candidate.request.action.id,
    findings: [
      {
        semanticKey: 'finding:model-only',
        text: '这是模型生成的解释，但没有外生证据。',
        confidence: 0.99,
      },
    ],
  })

  assert.equal(state(store, started.handle).Evidence.size, 0)
  assert.equal(investigated.request.type, 'GenerateCandidatesRequest')

  const regenerated = resume(store, started.handle, { type: 'Candidates', items: [] })
  assert.equal(regenerated.request.type, 'SynthesizeRequest')

  const answered = resume(store, started.handle, {
    type: 'Synthesis',
    text: '仅按当前未证实解释组织答案。',
    findingKeys: ['finding:model-only'],
  })
  assert.equal(answered.answer.epistemicBasis.evidence.length, 0)
  assert.equal(answered.answer.epistemicBasis.findings[0].confidence, null)
  assert.ok(answered.answer.uncertainties.includes('ungrounded-finding:finding:model-only'))
})

test('WHAT[EPI-005] synthesis_is_information_propagation_not_information_acquisition', () => {
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
  const before = state(store, started.handle).Evidence.size

  resume(store, started.handle, {
    type: 'Synthesis',
    text: '把已有发现组织为解释。',
    findingKeys: ['finding:mechanism'],
  })
  assert.equal(state(store, started.handle).Evidence.size, before)
})

test('WHAT[EPI-007] later_semantic_assessment_updates_control_belief_without_creating_evidence', () => {
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
  assert.equal(current.Evidence.size, 0)
  assert.equal(current.Findings.size, 0)
})

test('WHAT[EPI-008] gateway_gain_can_make_low_immediate_gain_question_worth_asking', () => {
  const store = createStore()
  const started = start(store, '复杂问题为什么发生？')
  assessWhy(store, started.handle)

  const result = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'UnknownExpansion',
        question: '哪个隐藏变量会打开后续判别问题？',
        semanticKey: 'question:gateway',
        expectedRootGain: 0.05,
        gatewayGain: 1,
        cost: 0.2,
      },
    ],
  })

  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'InvestigateRequest')
  assert.equal(result.request.action.semanticKey, 'question:gateway')
})
