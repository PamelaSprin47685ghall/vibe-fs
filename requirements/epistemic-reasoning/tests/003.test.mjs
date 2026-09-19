import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mcpServer } = await import("../../../dist/Sphinx/Surface.js");
const { createStore, start, resume, assessWhy } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-003] full_co_yield_path_preserves_grounded_epistemic_basis', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  const handle = started.handle
  assessWhy(store, handle)

  const candidate = resume(store, handle, {
    type: 'Candidates',
    items: [
      {
        method: 'CausalMechanism',
        question: '花青素合成及其光谱吸收是否解释红色？',
        semanticKey: 'question:anthocyanin',
        dependencyKey: 'source:pigment-study',
        expectedRootGain: 0.95,
        cost: 0.2,
      },
    ],
  })

  const investigated = resume(store, handle, {
    type: 'Investigation',
    actionKey: candidate.request.action.id,
    findings: [
      {
        semanticKey: 'finding:anthocyanin',
        text: '花青素的吸收谱与组织酸碱环境共同决定可见红色。',
        evidenceKeys: ['evidence:pigment-study'],
        provenance: ['investigation:pigment'],
      },
    ],
    evidence: [
      {
        semanticKey: 'evidence:pigment-study',
        proposition: '独立色素研究支持花青素机制。',
        source: { id: 'pigment-study', kind: 'document' },
        dependencyKey: 'pigment-study',
        provenance: ['document:pigment-study'],
      },
    ],
  })
  assert.equal(investigated.request.type, 'GenerateCandidatesRequest')

  const regenerated = resume(store, handle, {
    type: 'Candidates',
    items: [],
  })
  assert.equal(regenerated.request.type, 'SynthesizeRequest')

  const answered = resume(store, handle, {
    type: 'Synthesis',
    text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
    findingKeys: ['finding:anthocyanin'],
    uncertainties: [],
  })

  assert.equal(answered.status, 'answered')
  assert.equal(answered.handle, handle)
  assert.equal(answered.answer.epistemicBasis.evidence.length, 1)
  assert.equal(answered.answer.epistemicBasis.findings.length, 1)
  assert.equal(answered.answer.synthesis.findingKeys[0], 'finding:anthocyanin')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createStore, start, resume, state, assessWhy } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-003] ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence', () => {
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

  assert.equal(state(store, started.handle).evidence.length, 0)
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
}
