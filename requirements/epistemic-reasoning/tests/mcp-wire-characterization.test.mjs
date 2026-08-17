import { test } from 'node:test'
import assert from 'node:assert/strict'

import { createStore, start, resume, state, mcpServer } from '../../../dist/Sphinx/Surface.js'

test('WHAT[EPI-001] start_yield_wire_format_has_content_text_json', async () => {
  const server = mcpServer(createStore())
  const result = await server._registeredTools.start.handler({ question: '花青素合成是否解释红色？' })

  assert.equal(result.content[0].type, 'text')

  const parsed = JSON.parse(result.content[0].text)
  assert.equal(parsed.status, 'yield')
  assert.equal(typeof parsed.handle, 'string')
  assert.ok(parsed.handle.length > 0)
  assert.equal(parsed.request.type, 'SemanticAssessmentRequest')
})

test('WHAT[EPI-004] error_wire_format_has_status_and_error_string', async () => {
  const server = mcpServer(createStore())
  const started = await server._registeredTools.start.handler({ question: '花青素合成是否解释红色？' })
  const handle = JSON.parse(started.content[0].text).handle

  const result = await server._registeredTools.resume.handler({
    handle,
    observation: { type: 'Synthesis', text: 'wrong type', findingKeys: [] },
  })

  const parsed = JSON.parse(result.content[0].text)
  assert.equal(parsed.status, 'error')
  assert.equal(typeof parsed.error, 'string')
  assert.ok(parsed.error.length > 0)
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

test('WHAT[EPI-002] answered_wire_format_has_status_and_answer', async () => {
  const server = mcpServer(createStore())

  const started = JSON.parse(
    (await server._registeredTools.start.handler({ question: '花儿为什么这样红？' })).content[0].text,
  )
  const handle = started.handle

  await server._registeredTools.resume.handler({
    handle,
    observation: {
      type: 'SemanticAssessment',
      forms: { Why: 0.8, How: 0.2 },
      facets: { causal: 0.9, explanatory: 1 },
    },
  })

  const proposed = JSON.parse(
    (
      await server._registeredTools.resume.handler({
        handle,
        observation: {
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
        },
      })
    ).content[0].text,
  )

  await server._registeredTools.resume.handler({
    handle,
    observation: {
      type: 'Investigation',
      actionKey: proposed.request.action.id,
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
    },
  })

  await server._registeredTools.resume.handler({
    handle,
    observation: { type: 'Candidates', items: [] },
  })

  const answered = JSON.parse(
    (
      await server._registeredTools.resume.handler({
        handle,
        observation: {
          type: 'Synthesis',
          text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
          findingKeys: ['finding:anthocyanin'],
          uncertainties: [],
        },
      })
    ).content[0].text,
  )

  assert.equal(answered.status, 'answered')
  assert.equal(answered.handle, handle)
  assert.ok(answered.answer.question)
  assert.ok(answered.answer.contract)
  assert.ok(answered.answer.epistemicBasis)
  assert.equal(typeof answered.answer.revision, 'number')
})
