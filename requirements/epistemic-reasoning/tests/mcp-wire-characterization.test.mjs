import { test } from 'node:test'
import assert from 'node:assert/strict'

import { createStore, start, resume, state, mcpServer } from '../../../dist/Sphinx/Surface.js'

test('WHAT[EPI-001] start_yield_returns_structured_content_with_next_tool', async () => {
  const server = mcpServer(createStore())
  const result = await server._registeredTools.start.handler({ question: '花青素合成是否解释红色？' })

  assert.equal(result.isError, undefined)
  assert.equal(result.content[0].type, 'text')
  assert.match(result.content[0].text, /Next tool: assess/)

  const structured = result.structuredContent
  assert.equal(structured.status, 'yield')
  assert.equal(typeof structured.handle, 'string')
  assert.ok(structured.handle.length > 0)
  assert.equal(structured.revision, 0)
  assert.equal(structured.nextTool, 'assess')
  assert.equal(structured.request.type, 'SemanticAssessmentRequest')
  assert.equal(structured.answer, null)
})

test('WHAT[EPI-004] wrong_phase_returns_typed_error_without_structured_content', async () => {
  const server = mcpServer(createStore())
  const started = await server._registeredTools.start.handler({ question: '花青素合成是否解释红色？' })
  const handle = started.structuredContent.handle

  const result = await server._registeredTools.synthesize.handler({
    handle,
    text: 'wrong phase',
    findingKeys: [],
  })

  assert.equal(result.isError, true)
  assert.equal(result.structuredContent, undefined)
  assert.equal(result._meta.tool, 'synthesize')
  assert.equal(result._meta.error.code, 'KERNEL_REJECTED')
  assert.equal(result._meta.error.expectedTool, 'assess')
  assert.equal(result._meta.error.revision, 0)
  assert.equal(result._meta.error.handle, handle)
  assert.match(result.content[0].text, /KERNEL_REJECTED/)
})

test('WHAT[EPI-004] kernel_reject_does_not_advance_revision', () => {
  const store = createStore()
  const started = start(store, '花青素合成是否解释红色？')
  const handle = started.handle

  const beforeState = state(store, handle)
  assert.ok(beforeState, 'state should exist before error')
  const revisionBefore = beforeState.revision

  resume(store, handle, { type: 'Synthesis', text: 'wrong type', findingKeys: [] })

  const afterState = state(store, handle)
  assert.ok(afterState, 'state should still exist after error')
  assert.equal(afterState.revision, revisionBefore)
})

test('WHAT[EPI-002] answered_returns_structured_answer_and_null_next_tool', async () => {
  const server = mcpServer(createStore())
  const tools = server._registeredTools

  const started = await tools.start.handler({ question: '花儿为什么这样红？' })
  const handle = started.structuredContent.handle

  const assessed = await tools.assess.handler({
    handle,
    forms: { Why: 0.8, How: 0.2 },
    facets: { causal: 0.9, explanatory: 1 },
  })
  assert.equal(assessed.structuredContent.nextTool, 'propose')

  const proposed = await tools.propose.handler({
    handle,
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
  assert.equal(proposed.structuredContent.nextTool, 'investigate')
  const actionId = proposed.structuredContent.request.action.id

  const investigated = await tools.investigate.handler({
    handle,
    actionKey: actionId,
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
  assert.equal(investigated.structuredContent.status, 'yield')

  const regenerated = await tools.propose.handler({ handle, items: [] })
  assert.equal(regenerated.structuredContent.nextTool, 'synthesize')

  const answered = await tools.synthesize.handler({
    handle,
    text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
    findingKeys: ['finding:anthocyanin'],
    uncertainties: [],
  })

  assert.equal(answered.isError, undefined)
  assert.equal(answered.structuredContent.status, 'answered')
  assert.equal(answered.structuredContent.handle, handle)
  assert.equal(answered.structuredContent.nextTool, null)
  assert.equal(answered.structuredContent.request, null)
  assert.ok(answered.structuredContent.answer.question)
  assert.ok(answered.structuredContent.answer.contract)
  assert.ok(answered.structuredContent.answer.epistemicBasis)
  assert.equal(typeof answered.structuredContent.answer.revision, 'number')
})
