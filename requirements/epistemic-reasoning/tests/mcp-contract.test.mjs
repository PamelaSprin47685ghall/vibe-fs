import { test } from 'node:test'
import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'

import {
  createStore,
  start,
  resume,
  status,
  cancel,
  mcpServer,
} from '../../../dist/Sphinx/Surface.js'

// Shared flow fixtures (mirrors mcp-wire-characterization + support.mjs).
const ROOT_QUESTION = '花儿为什么这样红？'

const assessmentArgs = (handle) => ({
  handle,
  forms: { Why: 0.8, How: 0.2 },
  facets: { causal: 0.9, explanatory: 1 },
})

const assessmentObservation = {
  type: 'SemanticAssessment',
  forms: { Why: 0.8, How: 0.2 },
  facets: { causal: 0.9, explanatory: 1 },
}

const candidateItems = [
  {
    method: 'CausalMechanism',
    question: '花青素合成及其光谱吸收是否解释红色？',
    semanticKey: 'question:anthocyanin',
    dependencyKey: 'source:pigment-study',
    expectedRootGain: 0.95,
    cost: 0.2,
  },
]

const investigationArgs = (handle, actionKey) => ({
  handle,
  actionKey,
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

const synthesisArgs = (handle) => ({
  handle,
  text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
  findingKeys: ['finding:anthocyanin'],
  uncertainties: [],
})

// Drive a handler-layer inquiry all the way to `answered`. Returns the final
// (answered) result; callers may inspect intermediate steps themselves.
async function driveToAnswered(tools, handle) {
  await tools.assess.handler(assessmentArgs(handle))
  const proposed = await tools.propose.handler({ handle, items: candidateItems })
  const actionId = proposed.structuredContent.request.action.id
  await tools.investigate.handler(investigationArgs(handle, actionId))
  await tools.propose.handler({ handle, items: [] })
  return tools.synthesize.handler(synthesisArgs(handle))
}

test('WHAT[EPI-013] full_next_tool_chain_via_phase_tools', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle
  assert.equal(started.structuredContent.status, 'yield')
  assert.equal(started.structuredContent.nextTool, 'assess')
  assert.equal(started.structuredContent.revision, 0, 'revision is 0 right after start')

  const assessed = await tools.assess.handler(assessmentArgs(handle))
  assert.equal(assessed.structuredContent.status, 'yield')
  assert.equal(assessed.structuredContent.nextTool, 'propose')
  assert.equal(assessed.structuredContent.revision, 1)
  assert.ok(assessed.structuredContent.revision > started.structuredContent.revision)

  const proposed = await tools.propose.handler({ handle, items: candidateItems })
  assert.equal(proposed.structuredContent.status, 'yield')
  assert.equal(proposed.structuredContent.nextTool, 'investigate')
  assert.equal(proposed.structuredContent.revision, 2)
  assert.equal(proposed.structuredContent.request.action.id.length > 0, true)

  const investigated = await tools.investigate.handler(
    investigationArgs(handle, proposed.structuredContent.request.action.id),
  )
  assert.equal(investigated.structuredContent.status, 'yield')
  assert.equal(investigated.structuredContent.nextTool, 'propose')
  assert.equal(investigated.structuredContent.revision, 3)

  const regenerated = await tools.propose.handler({ handle, items: [] })
  assert.equal(regenerated.structuredContent.status, 'yield')
  assert.equal(regenerated.structuredContent.nextTool, 'synthesize')
  assert.equal(regenerated.structuredContent.revision, 4)

  const answered = await tools.synthesize.handler(synthesisArgs(handle))
  assert.equal(answered.isError, undefined)
  assert.equal(answered.structuredContent.status, 'answered')
  assert.equal(answered.structuredContent.handle, handle)
  assert.equal(answered.structuredContent.nextTool, null)
  assert.equal(answered.structuredContent.request, null)
  assert.equal(answered.structuredContent.revision, 5)
  assert.ok(answered.structuredContent.answer.question)
  assert.ok(answered.structuredContent.answer.contract)
  assert.ok(answered.structuredContent.answer.epistemicBasis)

  // Revision strictly increased at every step along the way.
  const revisions = [
    started.structuredContent.revision,
    assessed.structuredContent.revision,
    proposed.structuredContent.revision,
    investigated.structuredContent.revision,
    regenerated.structuredContent.revision,
    answered.structuredContent.revision,
  ]
  for (let i = 1; i < revisions.length; i++) {
    assert.ok(revisions[i] > revisions[i - 1], `revision step ${i} should increase`)
  }
})

test('WHAT[EPI-013] legacy_resume_advances_via_generic_decode_with_same_envelope', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const assessed = await tools.resume.handler({ handle, observation: assessmentObservation })

  // Same structured envelope as the phase-specific assess tool.
  assert.equal(assessed.isError, undefined)
  assert.equal(assessed.structuredContent.status, 'yield')
  assert.equal(assessed.structuredContent.handle, handle)
  assert.equal(assessed.structuredContent.nextTool, 'propose')
  assert.equal(assessed.structuredContent.request.type, 'GenerateCandidatesRequest')
  assert.equal(assessed.structuredContent.revision, 1)
})

test('WHAT[EPI-004] wrong_phase_returns_kernel_rejected_without_advancing', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const result = await tools.synthesize.handler(synthesisArgs(handle))

  assert.equal(result.isError, true)
  assert.equal(result.structuredContent, undefined)
  assert.equal(result._meta.tool, 'synthesize')
  assert.equal(result._meta.error.code, 'KERNEL_REJECTED')
  assert.equal(result._meta.error.expectedTool, 'assess')
  assert.equal(result._meta.error.revision, 0)
  assert.equal(result._meta.error.recoverable, true)
  assert.equal(result._meta.error.retryable, false)
  assert.equal(result._meta.error.handle, handle)

  // State must not have advanced.
  const statusResult = await tools.status.handler({ handle })
  assert.equal(statusResult.structuredContent.status, 'active')
  assert.equal(statusResult.structuredContent.revision, 0)
  assert.equal(statusResult.structuredContent.nextTool, 'assess')
})

test('WHAT[EPI-004] wrong_action_key_returns_kernel_rejected_revision_unchanged', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle
  await tools.assess.handler(assessmentArgs(handle))
  const proposed = await tools.propose.handler({ handle, items: candidateItems })
  const revisionBefore = proposed.structuredContent.revision
  assert.equal(proposed.structuredContent.nextTool, 'investigate')

  const result = await tools.investigate.handler(investigationArgs(handle, 'bogus'))

  assert.equal(result.isError, true)
  assert.equal(result.structuredContent, undefined)
  assert.equal(result._meta.tool, 'investigate')
  assert.equal(result._meta.error.code, 'KERNEL_REJECTED')
  assert.equal(result._meta.error.expectedTool, 'investigate')
  assert.equal(result._meta.error.revision, revisionBefore)
  assert.equal(result._meta.error.recoverable, true)
  assert.equal(result._meta.error.retryable, false)

  // Revision unchanged: status still at the investigate phase.
  const statusResult = await tools.status.handler({ handle })
  assert.equal(statusResult.structuredContent.revision, revisionBefore)
  assert.equal(statusResult.structuredContent.nextTool, 'investigate')
})

test('WHAT[EPI-013] invalid_observation_when_forms_missing', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const result = await tools.assess.handler({ handle, facets: { causal: 0.9 } })

  assert.equal(result.isError, true)
  assert.equal(result.structuredContent, undefined)
  assert.equal(result._meta.tool, 'assess')
  assert.equal(result._meta.error.code, 'INVALID_OBSERVATION')
  assert.match(result._meta.error.message, /forms/i)
  assert.equal(result._meta.error.recoverable, true)
  assert.equal(result._meta.error.retryable, false)
})

test('WHAT[EPI-013] missing_handle_question_required_unknown_handle_codes', async () => {
  const tools = mcpServer(createStore())._registeredTools

  // MISSING_HANDLE: blank handle on a phase tool whose observation decodes ok.
  const missing = await tools.assess.handler({
    handle: '',
    forms: { Why: 0.8, How: 0.2 },
    facets: { causal: 0.9 },
  })
  assert.equal(missing.isError, true)
  assert.equal(missing._meta.error.code, 'MISSING_HANDLE')
  assert.equal(missing._meta.error.recoverable, true)

  // QUESTION_REQUIRED: blank (whitespace-only) start question.
  const rejected = await tools.start.handler({ question: '  ' })
  assert.equal(rejected.isError, true)
  assert.equal(rejected._meta.error.code, 'QUESTION_REQUIRED')
  assert.equal(rejected._meta.error.recoverable, true)
  assert.equal(rejected._meta.error.retryable, false)

  // UNKNOWN_HANDLE: status on a random uuid that was never started.
  const unknown = await tools.status.handler({ handle: randomUUID() })
  assert.equal(unknown.isError, true)
  assert.equal(unknown._meta.error.code, 'UNKNOWN_HANDLE')
  assert.equal(unknown._meta.error.recoverable, false)
  assert.equal(unknown._meta.error.retryable, false)
})

test('WHAT[EPI-002] terminal_answered_rejects_further_observations', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle
  const answered = await driveToAnswered(tools, handle)
  assert.equal(answered.structuredContent.status, 'answered')

  const result = await tools.propose.handler({ handle, items: candidateItems })

  assert.equal(result.isError, true)
  assert.equal(result.structuredContent, undefined)
  assert.equal(result._meta.tool, 'propose')
  assert.equal(result._meta.error.code, 'ALREADY_ANSWERED')
  assert.equal(result._meta.error.recoverable, false)
  assert.equal(result._meta.error.retryable, false)
  assert.equal(result._meta.error.handle, handle)

  // The completed answer is still served by status.
  const statusResult = await tools.status.handler({ handle })
  assert.equal(statusResult.structuredContent.status, 'answered')
  assert.equal(statusResult.structuredContent.nextTool, null)
  assert.ok(statusResult.structuredContent.answer.question)
})

test('WHAT[EPI-002] cancel_releases_handle_and_makes_it_unknown', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const cancelled = await tools.cancel.handler({ handle })
  assert.equal(cancelled.isError, undefined)
  assert.equal(cancelled.structuredContent.handle, handle)
  assert.equal(cancelled.structuredContent.status, 'cancelled')

  // After cancel, the handle is unknown to both status and phase tools.
  const statusAfter = await tools.status.handler({ handle })
  assert.equal(statusAfter.isError, true)
  assert.equal(statusAfter._meta.error.code, 'UNKNOWN_HANDLE')

  const assessAfter = await tools.assess.handler(assessmentArgs(handle))
  assert.equal(assessAfter.isError, true)
  assert.equal(assessAfter._meta.error.code, 'UNKNOWN_HANDLE')
})

test('WHAT[EPI-013] surface_status_and_cancel_functions_match_handler_envelopes', () => {
  const store = createStore()
  const handle = start(store, ROOT_QUESTION).handle

  // Active payload right after start.
  const active = status(store, handle)
  assert.equal(active.status, 'active')
  assert.equal(active.handle, handle)
  assert.equal(active.revision, 0)
  assert.equal(active.nextTool, 'assess')
  assert.equal(active.request.type, 'SemanticAssessmentRequest')

  // Drive the store via the surface resume to answered.
  resume(store, handle, assessmentObservation)
  const candidate = resume(store, handle, { type: 'Candidates', items: candidateItems })
  resume(store, handle, {
    type: 'Investigation',
    actionKey: candidate.request.action.id,
    findings: investigationArgs(handle, candidate.request.action.id).findings,
    evidence: investigationArgs(handle, candidate.request.action.id).evidence,
  })
  resume(store, handle, { type: 'Candidates', items: [] })
  resume(store, handle, {
    type: 'Synthesis',
    text: synthesisArgs(handle).text,
    findingKeys: synthesisArgs(handle).findingKeys,
    uncertainties: [],
  })

  const answered = status(store, handle)
  assert.equal(answered.status, 'answered')
  assert.equal(answered.handle, handle)
  assert.equal(answered.nextTool, null)
  assert.equal(answered.request, null)
  assert.ok(answered.answer.question)
  assert.ok(answered.answer.contract)
  assert.ok(answered.answer.epistemicBasis)

  // Cancel releases the handle.
  const cancelled = cancel(store, handle)
  assert.equal(cancelled.status, 'cancelled')
  assert.equal(cancelled.handle, handle)

  // Then status returns an error object with UNKNOWN_HANDLE.
  const afterCancel = status(store, handle)
  assert.equal(afterCancel.code, 'UNKNOWN_HANDLE')
  assert.equal(afterCancel.recoverable, false)
})

test('WHAT[EPI-013] kernel_rejected_error_content_is_human_readable', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const result = await tools.synthesize.handler(synthesisArgs(handle))

  assert.equal(result.isError, true)
  assert.equal(result.content[0].type, 'text')
  assert.match(result.content[0].text, /KERNEL_REJECTED/)
  assert.match(result.content[0].text, /Next action/)
})
