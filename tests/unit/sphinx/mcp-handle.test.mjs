import assert from 'node:assert/strict'
import test from 'node:test'
import { createSessionStore } from '../../../src/sphinx/session.js'
import { createSphinxMcpServer } from '../../../src/sphinx/mcp-server.js'

test('start_returns_opaque_handle_and_semantic_request', () => {
  const store = createSessionStore()
  const out = store.start('花儿为什么这样红？')
  assert.equal(out.status, 'yield')
  assert.equal(typeof out.handle, 'string')
  assert.match(out.handle, /^[0-9a-f-]{36}$/i)
  assert.equal(out.request.type, 'SemanticAssessmentRequest')
  assert.equal(out.request.question, '花儿为什么这样红？')
})

test('resume_missing_or_unknown_handle_fails', () => {
  const store = createSessionStore()
  assert.deepEqual(store.resume('', { type: 'SemanticAssessment', forms: { Why: 1 } }), {
    status: 'error',
    error: 'missing handle',
  })
  assert.deepEqual(store.resume(null, { type: 'SemanticAssessment', forms: { Why: 1 } }), {
    status: 'error',
    error: 'missing handle',
  })
  const bad = store.resume('00000000-0000-4000-8000-000000000000', {
    type: 'SemanticAssessment',
    forms: { Why: 1 },
  })
  assert.equal(bad.status, 'error')
  assert.equal(bad.error, 'unknown handle')
})

test('full_start_resume_path_reaches_answered', () => {
  const store = createSessionStore()
  const started = store.start('花儿为什么这样红？')
  assert.equal(started.status, 'yield')
  const handle = started.handle

  const assessed = store.resume(handle, {
    type: 'SemanticAssessment',
    forms: { Why: 0.75, How: 0.18, Other: 0.07 },
    facets: { causal: 0.84, explanatory: 0.91, predictive: 0.06 },
  })
  assert.equal(assessed.status, 'yield')
  assert.equal(assessed.handle, handle)
  assert.equal(assessed.request.type, 'GenerateCandidatesRequest')
  assert.ok(assessed.request.methods.includes('Multidisciplinary'))

  const candidated = store.resume(handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Multidisciplinary',
        text: 'Anthocyanin pigment chemistry',
        semanticKey: 'multi:anthocyanin',
      },
      {
        method: 'Abduction',
        text: 'Pollinator attraction hypothesis',
        semanticKey: 'abd:pollinator',
      },
      {
        method: 'Counterexample',
        text: 'White cultivars under same genes',
        semanticKey: 'cex:white',
      },
    ],
  })
  assert.equal(candidated.status, 'yield')
  assert.equal(candidated.request.type, 'EstimateValueRequest')
  assert.ok(candidated.request.actions.length >= 1)

  const estimates = candidated.request.actions.map((action, index) => ({
    actionId: action.id,
    rootRelativeValue: 0.9 - index * 0.05,
  }))
  const valued = store.resume(handle, {
    type: 'ValueEstimates',
    estimates,
  })
  assert.equal(valued.status, 'yield')
  assert.equal(valued.request.type, 'SynthesizeRequest')
  assert.ok(valued.request.strands.length >= 1)

  const answered = store.resume(handle, {
    type: 'Synthesis',
    text: 'Redness is jointly explained by pigment chemistry and ecological signaling; white cultivars mark boundary conditions.',
    strands: valued.request.strands.map((s) => s.semanticKey),
  })
  assert.equal(answered.status, 'answered')
  assert.equal(answered.handle, handle)
  assert.equal(answered.answer.question, '花儿为什么这样红？')
  assert.equal(answered.answer.contract.primaryForm, 'Why')
  assert.match(answered.answer.synthesis.text, /pigment/)
  assert.ok(answered.answer.evidenceMass > 0)
  assert.ok(['stop-dominates', 'assembled'].includes(answered.answer.stopReason))
})

test('mcp_server_registers_start_and_resume_tools', async () => {
  const store = createSessionStore()
  const server = createSphinxMcpServer(store)
  const tools = Object.keys(server._registeredTools).sort()
  assert.deepEqual(tools, ['resume', 'start'])
  assert.equal(server._registeredTools.start.inputSchema.type, 'object')
  assert.equal(server._registeredTools.resume.inputSchema.type, 'object')

  const started = JSON.parse(
    (await server._registeredTools.start.handler({ question: '明天白银会涨吗？' })).content[0].text,
  )
  assert.equal(started.status, 'yield')
  assert.equal(typeof started.handle, 'string')
  assert.equal(started.request.type, 'SemanticAssessmentRequest')

  const resumed = JSON.parse(
    (
      await server._registeredTools.resume.handler({
        handle: started.handle,
        observation: { type: 'SemanticAssessment', forms: { Polar: 0.9 }, facets: { predictive: 0.8 } },
      })
    ).content[0].text,
  )
  assert.equal(resumed.handle, started.handle)
  assert.equal(resumed.status, 'yield')
})
