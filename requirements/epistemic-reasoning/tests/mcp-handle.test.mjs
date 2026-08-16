import assert from 'node:assert/strict'
import test from 'node:test'

import { createMcpServer, createStore, start, resume, assessWhy } from './support.mjs'

test('WHAT[EPI-002] handle_is_opaque_process_local_session_key', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assert.equal(typeof started.handle, 'string')
  assert.match(started.handle, /^[0-9a-f-]{36}$/i)

  assert.equal(resume(store, '', { type: 'SemanticAssessment', forms: { Why: 1 } }).error, 'missing handle')
  assert.equal(
    resume(store, '00000000-0000-4000-8000-000000000000', {
      type: 'SemanticAssessment',
      forms: { Why: 1 },
    }).error,
    'unknown handle',
  )
})

test('WHAT[EPI-002] full_co_yield_path_preserves_kernel_continuation', () => {
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
  assert.equal(candidate.request.type, 'InvestigateRequest')

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
  assert.equal(investigated.status, 'yield')
  assert.equal(investigated.request.type, 'GenerateCandidatesRequest')

  const regenerated = resume(store, handle, {
    type: 'Candidates',
    items: [],
  })
  assert.equal(regenerated.status, 'yield')
  assert.equal(regenerated.request.type, 'SynthesizeRequest')
})

test('WHAT[EPI-003] full_co_yield_path_preserves_grounded_epistemic_basis', () => {
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

test('WHAT[EPI-004] mcp_server_surface_is_exactly_start_and_resume', async () => {
  const server = createMcpServer(createStore())
  assert.ok('start' in server._registeredTools)
  assert.ok('resume' in server._registeredTools)
  assert.equal(Object.getOwnPropertyNames(server._registeredTools).length, 2)

  const started = JSON.parse(
    (await server._registeredTools.start.handler({ question: '明天白银会涨吗？' })).content[0].text,
  )
  assert.equal(started.status, 'yield')
  assert.equal(started.request.type, 'SemanticAssessmentRequest')

  const resumed = JSON.parse(
    (
      await server._registeredTools.resume.handler({
        handle: started.handle,
        observation: {
          type: 'SemanticAssessment',
          forms: { Polar: 0.9, Other: 0.1 },
          facets: { predictive: 1 },
        },
      })
    ).content[0].text,
  )
  assert.equal(resumed.handle, started.handle)
  assert.equal(resumed.request.type, 'GenerateCandidatesRequest')
})
