import test from 'node:test'

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { randomUUID } = await import("node:crypto");
const { createStore, start, resume, status, cancel, mcpServer } = await import("../../../dist/Sphinx/Surface.js");

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
test('WHAT[EPI-013] full_next_tool_chain_with_bare_candidate_proposals', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: '如何写程序才能写好程序？' })
  const handle = started.structuredContent.handle
  assert.equal(started.structuredContent.status, 'yield')
  assert.equal(started.structuredContent.nextTool, 'assess')

  const assessed = await tools.assess.handler({
    handle,
    forms: { How: 0.8, What: 0.1, Why: 0.1 },
    targets: ['写程序', '好程序'],
    intents: ['方法', '实践', '原则'],
  })
  assert.equal(assessed.structuredContent.status, 'yield')
  assert.equal(assessed.structuredContent.nextTool, 'propose')

  const proposed = await tools.propose.handler({
    handle,
    items: [
      {
        method: 'CausalMechanism',
        question: '决定程序好坏的因果机制是什么？',
        semanticKey: 'causal-mechanism',
      },
    ],
  })
  assert.equal(proposed.structuredContent.status, 'yield')
  assert.equal(proposed.structuredContent.nextTool, 'investigate')
  const actionId = proposed.structuredContent.request.action.id
  assert.ok(actionId.length > 0)

  const investigated = await tools.investigate.handler({
    handle,
    actionKey: actionId,
    findings: [
      {
        semanticKey: 'finding:modularity',
        text: '模块化与低耦合是程序长期可维护性的因果基础。',
        evidenceKeys: ['evidence:software-engineering-study'],
      },
    ],
    evidence: [
      {
        semanticKey: 'evidence:software-engineering-study',
        proposition: '实证研究表明低耦合架构的缺陷率显著更低。',
        source: { id: 'se-empirical-study', kind: 'document' },
        dependencyKey: 'study-1',
      },
    ],
  })
  assert.equal(investigated.structuredContent.status, 'yield')
  assert.equal(investigated.structuredContent.nextTool, 'propose')

  const regenerated = await tools.propose.handler({ handle, items: [] })
  assert.equal(regenerated.structuredContent.status, 'yield')
  assert.equal(regenerated.structuredContent.nextTool, 'synthesize')

  const answered = await tools.synthesize.handler({
    handle,
    text: '写好程序需要遵循低耦合高内聚原则，通过持续重构和测试保证质量。',
    findingKeys: ['finding:modularity'],
    uncertainties: [],
  })
  assert.equal(answered.isError, undefined)
  assert.equal(answered.structuredContent.status, 'answered')
  assert.equal(answered.structuredContent.answer.stopReason, 'stop-dominates')
  assert.equal(answered.structuredContent.answer.epistemicBasis.findings.length, 1)
  assert.equal(answered.structuredContent.answer.epistemicBasis.evidence.length, 1)
})
test('WHAT[EPI-013] generic_tools_registered_alongside_legacy_eight', () => {
  const tools = mcpServer(createStore())._registeredTools

  for (const name of ['start', 'assess', 'propose', 'investigate', 'synthesize', 'status', 'cancel', 'resume']) {
    assert.ok(name in tools, `legacy tool ${name} must stay registered`)
  }
  for (const name of [
    'sphinx_inquiry_start',
    'sphinx_work_submit',
    'sphinx_inquiry_status',
    'sphinx_inquiry_export',
    'sphinx_inquiry_cancel',
  ]) {
    assert.ok(name in tools, `generic tool ${name} must be registered`)
    assert.equal(typeof tools[name].handler, 'function')
  }
})
test('WHAT[EPI-013] generic_start_status_cancel_envelope_with_iq_ids_and_stale_submit_conflict', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.sphinx_inquiry_start.handler({
    question: ROOT_QUESTION,
    profile: 'default-legacy',
    plugins: [],
    executionMode: 'mcp',
    budget: {},
  })
  assert.equal(started.isError, undefined)
  assert.match(started.structuredContent.inquiryId, /^iq_/)
  assert.equal(started.structuredContent.revision, 0)

  const status = await tools.sphinx_inquiry_status.handler({
    inquiryId: started.structuredContent.inquiryId,
  })
  assert.equal(status.isError, undefined)
  assert.equal(status.structuredContent.inquiryId, started.structuredContent.inquiryId)

  // Stale expectedRevision conflicts before any write, mirroring the
  // EventStore rule: the failed submit advances nothing.
  const stale = await tools.sphinx_work_submit.handler({
    inquiryId: started.structuredContent.inquiryId,
    expectedRevision: 999,
    results: [],
  })
  assert.equal(stale.isError, true)
  assert.match(stale._meta.error.code, /REVISION_CONFLICT/)
  const after = await tools.sphinx_inquiry_status.handler({
    inquiryId: started.structuredContent.inquiryId,
  })
  assert.equal(after.structuredContent.revision, status.structuredContent.revision)

  const cancelled = await tools.sphinx_inquiry_cancel.handler({
    inquiryId: started.structuredContent.inquiryId,
  })
  assert.equal(cancelled.isError, undefined)
  assert.equal(cancelled.structuredContent.status, 'cancelled')

  const gone = await tools.sphinx_inquiry_status.handler({
    inquiryId: started.structuredContent.inquiryId,
  })
  assert.equal(gone.isError, true)
})
test('WHAT[EPI-013] text_outputs_carry_handle_and_inquiry_id_for_text_only_models', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle
  assert.match(started.content[0].text, new RegExp(handle))
  const status = await tools.status.handler({ handle })
  assert.match(status.content[0].text, new RegExp(handle))

  const generic = await tools.sphinx_inquiry_start.handler({ question: ROOT_QUESTION })
  const inquiryId = generic.structuredContent.inquiryId
  assert.match(generic.content[0].text, new RegExp(inquiryId))
  const gstatus = await tools.sphinx_inquiry_status.handler({ inquiryId })
  assert.match(gstatus.content[0].text, new RegExp(inquiryId))

  const unknown = await tools.sphinx_inquiry_status.handler({ inquiryId: 'iq_nope' })
  assert.equal(unknown.isError, true)
  assert.match(unknown.content[0].text, /iq_nope/)
  assert.equal(unknown._meta.error.code, 'UNKNOWN_HANDLE')
})
test('WHAT[EPI-013] empty_forms_assessment_abstains_but_advances', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.start.handler({ question: ROOT_QUESTION })
  const handle = started.structuredContent.handle

  const assessed = await tools.assess.handler({ handle, forms: {} })
  assert.equal(assessed.isError, undefined)
  assert.equal(assessed.structuredContent.status, 'yield')
  assert.equal(assessed.structuredContent.nextTool, 'propose')
  assert.equal(assessed.structuredContent.revision, 1)
})
test('WHAT[EPI-013] generic_cancel_reports_cancelled_code_and_blank_start_names_its_tool', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const blank = await tools.sphinx_inquiry_start.handler({ question: '  ' })
  assert.equal(blank.isError, true)
  assert.equal(blank._meta.error.code, 'QUESTION_REQUIRED')
  assert.match(blank.content[0].text, /sphinx_inquiry_start/)

  const started = await tools.sphinx_inquiry_start.handler({ question: ROOT_QUESTION })
  const inquiryId = started.structuredContent.inquiryId
  const cancelled = await tools.sphinx_inquiry_cancel.handler({ inquiryId })
  assert.equal(cancelled.isError, undefined)

  const statusAfter = await tools.sphinx_inquiry_status.handler({ inquiryId })
  assert.equal(statusAfter.isError, true)
  assert.equal(statusAfter._meta.error.code, 'inquiry-cancelled')
  assert.match(statusAfter.content[0].text, new RegExp(inquiryId))

  const submitAfter = await tools.sphinx_work_submit.handler({ inquiryId, expectedRevision: 0, results: [] })
  assert.equal(submitAfter.isError, true)
  assert.equal(submitAfter._meta.error.code, 'inquiry-cancelled')
})
test('WHAT[EPI-013] legacy_shaped_id_in_generic_tool_gets_iq_hint', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const legacyShaped = await tools.sphinx_inquiry_status.handler({ inquiryId: '0702fb48-6517-4875-8fee-33ed6ad2cc38' })
  assert.equal(legacyShaped.isError, true)
  assert.match(legacyShaped.content[0].text, /iq_ ids from sphinx_inquiry_start/)
})
test('WHAT[EPI-013] generic_submit_with_results_advances_revision_and_status_follows', async () => {
  const tools = mcpServer(createStore())._registeredTools

  const started = await tools.sphinx_inquiry_start.handler({
    question: ROOT_QUESTION,
    profile: 'default-legacy',
    plugins: [],
    executionMode: 'mcp',
    budget: {},
  })
  assert.equal(started.isError, undefined)
  const inquiryId = started.structuredContent.inquiryId

  const first = await tools.sphinx_work_submit.handler({
    inquiryId,
    expectedRevision: 0,
    results: [{ workId: 'work_001', attempt: 1, payload: { text: 'first observation' } }],
  })
  assert.equal(first.isError, undefined)
  assert.equal(first.structuredContent.revision, 1)

  const mid = await tools.sphinx_inquiry_status.handler({ inquiryId })
  assert.equal(mid.isError, undefined)
  assert.equal(mid.structuredContent.revision, 1)

  const second = await tools.sphinx_work_submit.handler({
    inquiryId,
    expectedRevision: 1,
    results: [{ workId: 'work_002', attempt: 1, payload: { text: 'second observation' } }],
  })
  assert.equal(second.isError, undefined)
  assert.equal(second.structuredContent.revision, 2)

  // The first revision is now stale: replaying it conflicts without advancing.
  const replayed = await tools.sphinx_work_submit.handler({
    inquiryId,
    expectedRevision: 1,
    results: [],
  })
  assert.equal(replayed.isError, true)
  assert.match(replayed._meta.error.code, /REVISION_CONFLICT/)
  const held = await tools.sphinx_inquiry_status.handler({ inquiryId })
  assert.equal(held.structuredContent.revision, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mcpServer } = await import("../../../dist/Sphinx/Surface.js");
const { createStore, start, resume, assessWhy } = await import("./support.mjs");


test('WHAT[EPI-013] mcp_server_surface_exposes_phase_tools_and_legacy_resume', async () => {
  const server = mcpServer(createStore())
  for (const name of ['start', 'assess', 'propose', 'investigate', 'synthesize', 'status', 'cancel']) {
    assert.ok(name in server._registeredTools, `missing tool ${name}`)
  }
  assert.ok('resume' in server._registeredTools, 'legacy resume must remain for compatibility')
  assert.match(server._registeredTools.resume.description, /^Legacy compatibility tool/)

  const started = await server._registeredTools.start.handler({ question: '明天白银会涨吗？' })
  assert.equal(started.structuredContent.status, 'yield')
  assert.equal(started.structuredContent.nextTool, 'assess')
  assert.equal(started.structuredContent.request.type, 'SemanticAssessmentRequest')

  const assessed = await server._registeredTools.assess.handler({
    handle: started.structuredContent.handle,
    forms: { Polar: 0.9, Other: 0.1 },
    facets: { predictive: 1 },
  })
  assert.equal(assessed.structuredContent.handle, started.structuredContent.handle)
  assert.equal(assessed.structuredContent.nextTool, 'propose')
  assert.equal(assessed.structuredContent.request.type, 'GenerateCandidatesRequest')
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { spawn } = await import("node:child_process");
const { readFileSync } = await import("node:fs");
const { mkdtemp, rm } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { randomUUID } = await import("node:crypto");
const { fileURLToPath } = await import("node:url");
const { dirname, join } = await import("node:path");
const { relativeServerEntry } = await import("./support.mjs");

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const serverEntry = join(repoRoot, relativeServerEntry)
const pkgVersion = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')).version
const QUESTION = '花青素合成是否解释红色？'
const CANDIDATE = {
  method: 'CausalMechanism',
  question: '花青素合成及其光谱吸收是否解释红色？',
  semanticKey: 'question:anthocyanin',
  dependencyKey: 'source:pigment-study',
  expectedRootGain: 0.95,
  cost: 0.2,
}
const FINDINGS = [
  {
    semanticKey: 'finding:anthocyanin',
    text: '花青素的吸收谱与组织酸碱环境共同决定可见红色。',
    evidenceKeys: ['evidence:pigment-study'],
    provenance: ['investigation:pigment'],
  },
]
const EVIDENCE = [
  {
    semanticKey: 'evidence:pigment-study',
    proposition: '独立色素研究支持花青素机制。',
    source: { id: 'pigment-study', kind: 'document' },
    dependencyKey: 'pigment-study',
    provenance: ['document:pigment-study'],
  },
]
const SYNTHESIS = {
  text: '现有证据支持以花青素机制解释红色，同时保留环境条件作为边界。',
  findingKeys: ['finding:anthocyanin'],
  uncertainties: [],
}
function assessArgs(handle) {
  return { handle, forms: { Why: 0.8, How: 0.2 }, facets: { causal: 0.9, explanatory: 1 } }
}
function investigateArgs(handle, actionKey) {
  return { handle, actionKey, findings: FINDINGS, evidence: EVIDENCE }
}
function spawnSphinx(env = {}) {
  const proc = spawn('node', [serverEntry], {
    cwd: repoRoot,
    stdio: ['pipe', 'pipe', 'pipe'],
    env: { ...process.env, ...env },
  })

  let nextId = 1
  /** @type {Map<number, {resolve:(v:any)=>void, reject:(e:Error)=>void, timer:NodeJS.Timeout}>} */
  const pending = new Map()
  let stdoutBuf = ''
  const lines = []
  let stderrBuf = ''

  proc.stdout.setEncoding('utf8')
  proc.stdout.on('data', (chunk) => {
    stdoutBuf += chunk
    let idx
    while ((idx = stdoutBuf.indexOf('\n')) !== -1) {
      const line = stdoutBuf.slice(0, idx).replace(/\r$/, '')
      stdoutBuf = stdoutBuf.slice(idx + 1)
      if (line.length === 0) continue
      lines.push(line)
      let msg
      try {
        msg = JSON.parse(line)
      } catch {
        continue
      }
      if (msg && msg.id != null && pending.has(msg.id)) {
        const p = pending.get(msg.id)
        pending.delete(msg.id)
        clearTimeout(p.timer)
        p.resolve(msg)
      }
    }
  })

  proc.stderr.setEncoding('utf8')
  proc.stderr.on('data', (chunk) => {
    stderrBuf += chunk
  })

  proc.on('error', () => {})
  proc.on('exit', (code) => {
    for (const [, p] of pending) {
      clearTimeout(p.timer)
      p.reject(new Error(`sphinx server exited with code ${code}\nstderr:\n${stderrBuf}`))
    }
    pending.clear()
  })

  function write(msg) {
    try {
      proc.stdin.write(JSON.stringify(msg) + '\n')
    } catch {
      /* process may already be dead */
    }
  }

  function call(method, params, timeoutMs = 15000) {
    const id = nextId++
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id)
        reject(new Error(`timeout waiting for ${method} id=${id}\nstderr:\n${stderrBuf}`))
      }, timeoutMs)
      pending.set(id, { resolve, reject, timer })
      write({ jsonrpc: '2.0', id, method, params: params ?? {} })
    })
  }

  function notify(method, params) {
    write({ jsonrpc: '2.0', method, params: params ?? {} })
  }

  async function initialize() {
    const res = await call('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'blackbox-test', version: '0.0.0' },
    })
    notify('notifications/initialized', {})
    return res
  }

  function tool(name, args, timeoutMs) {
    return call('tools/call', { name, arguments: args }, timeoutMs)
  }

  function close() {
    pending.clear()
    try {
      proc.kill('SIGKILL')
    } catch {
      /* already dead */
    }
  }

  return {
    proc,
    call,
    notify,
    initialize,
    tool,
    close,
    get lines() {
      return lines
    },
    get stderr() {
      return stderrBuf
    },
  }
}
async function driveToAnswered(s) {
  const startRes = await s.tool('start', { question: QUESTION })
  const handle = startRes.result.structuredContent.handle
  let nextTool = startRes.result.structuredContent.nextTool
  let actionKey = null
  let investigated = false

  while (nextTool != null) {
    let res
    if (nextTool === 'assess') {
      res = await s.tool('assess', assessArgs(handle))
    } else if (nextTool === 'propose') {
      const items = investigated ? [] : [CANDIDATE]
      res = await s.tool('propose', { handle, items })
    } else if (nextTool === 'investigate') {
      res = await s.tool('investigate', investigateArgs(handle, actionKey))
      investigated = true
    } else if (nextTool === 'synthesize') {
      res = await s.tool('synthesize', { handle, ...SYNTHESIS })
    } else {
      throw new Error(`unexpected nextTool: ${nextTool}`)
    }
    const sc = res.result.structuredContent
    nextTool = sc.nextTool
    if (sc.request?.action?.id) actionKey = sc.request.action.id
    if (sc.status === 'answered') break
  }
  return handle
}

test('WHAT[EPI-013] tools_list_returns_legacy_eight_plus_generic_five_with_schemas', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    // Default blackbox client negotiates 2024-11-05: the legacy eight keep
    // working there and the generic five are listed alongside them.
    await s.initialize()
    const res = await s.call('tools/list', {})
    const names = res.result.tools.map((t) => t.name)
    assert.deepEqual([...names].sort(), [
      'assess',
      'cancel',
      'investigate',
      'propose',
      'resume',
      'sphinx_inquiry_cancel',
      'sphinx_inquiry_export',
      'sphinx_inquiry_start',
      'sphinx_inquiry_status',
      'sphinx_work_submit',
      'start',
      'status',
      'synthesize',
    ])
    for (const t of res.result.tools) {
      assert.ok(t.inputSchema, `${t.name} must have an inputSchema`)
      assert.ok(
        typeof t.description === 'string' && t.description.length > 0,
        `${t.name} must have a description`,
      )
    }
    const resume = res.result.tools.find((t) => t.name === 'resume')
    assert.match(resume.description, /^Legacy compatibility tool/)
    const investigate = res.result.tools.find((t) => t.name === 'investigate')
    assert.ok(
      investigate.inputSchema.required?.includes('actionKey'),
      'investigate schema must require actionKey',
    )
    const genericStart = res.result.tools.find((t) => t.name === 'sphinx_inquiry_start')
    assert.ok(
      genericStart.inputSchema.required?.includes('question'),
      'generic start schema must require question',
    )
    for (const field of ['profile', 'plugins', 'executionMode', 'budget']) {
      assert.ok(
        genericStart.inputSchema.properties && field in genericStart.inputSchema.properties,
        `generic start schema must accept ${field}`,
      )
    }
    const submit = res.result.tools.find((t) => t.name === 'sphinx_work_submit')
    for (const field of ['inquiryId', 'expectedRevision', 'results']) {
      assert.ok(
        submit.inputSchema.required?.includes(field),
        `generic submit schema must require ${field}`,
      )
    }
  } finally {
    s.close()
  }
})
test('WHAT[EPI-013] full_flow_to_answered_driven_by_next_tool', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    const startRes = await s.tool('start', { question: QUESTION })
    const handle = startRes.result.structuredContent.handle
    const seq = []
    let nextTool = startRes.result.structuredContent.nextTool
    if (nextTool) seq.push(nextTool)
    let actionKey = null
    let investigated = false

    while (nextTool != null) {
      let res
      if (nextTool === 'assess') {
        res = await s.tool('assess', assessArgs(handle))
      } else if (nextTool === 'propose') {
        const items = investigated ? [] : [CANDIDATE]
        res = await s.tool('propose', { handle, items })
      } else if (nextTool === 'investigate') {
        res = await s.tool('investigate', investigateArgs(handle, actionKey))
        investigated = true
      } else if (nextTool === 'synthesize') {
        res = await s.tool('synthesize', { handle, ...SYNTHESIS })
      } else {
        throw new Error(`unexpected nextTool: ${nextTool}`)
      }
      const sc = res.result.structuredContent
      nextTool = sc.nextTool
      if (sc.request?.action?.id) actionKey = sc.request.action.id
      if (nextTool) seq.push(nextTool)
      if (sc.status === 'answered') {
        assert.ok(sc.answer, 'answered result must have an answer')
        assert.equal(sc.answer.epistemicBasis.findings.length, 1)
        break
      }
    }

    assert.deepEqual(seq, ['assess', 'propose', 'investigate', 'propose', 'synthesize'])
  } finally {
    s.close()
  }
})
test('WHAT[EPI-013] unknown_handle_and_malformed_payload_are_typed_errors', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()

    // unknown handle → UNKNOWN_HANDLE
    const st = await s.tool('status', { handle: randomUUID() })
    assert.equal(st.result.isError, true)
    assert.equal(st.result.structuredContent, undefined)
    assert.equal(st.result._meta.error.code, 'UNKNOWN_HANDLE')

    // malformed payload: forms as a string (zod rejects → protocol-level error,
    // or codec rejects → INVALID_OBSERVATION).  Either way it must be an error.
    const startRes = await s.tool('start', { question: QUESTION })
    const handle = startRes.result.structuredContent.handle
    const bad = await s.tool('assess', { handle, forms: 'not-a-record' })
    assert.equal(bad.result.isError, true)
    assert.equal(bad.result.structuredContent, undefined)
    if (bad.result._meta?.error?.code) {
      assert.equal(bad.result._meta.error.code, 'INVALID_OBSERVATION')
    }

    // server still answers a following status call (no crash)
    const ok = await s.tool('status', { handle })
    assert.equal(ok.result.isError, undefined)
    assert.equal(ok.result.structuredContent.status, 'active')
  } finally {
    s.close()
  }
})
test('WHAT[EPI-013] answered_then_submit_returns_already_answered', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    const handle = await driveToAnswered(s)

    const res = await s.tool('propose', { handle, items: [] })
    assert.equal(res.result.isError, true)
    assert.equal(res.result.structuredContent, undefined)
    assert.equal(res.result._meta.error.code, 'ALREADY_ANSWERED')
  } finally {
    s.close()
  }
})
test('WHAT[EPI-013] stdout_lines_are_pure_jsonrpc', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    await driveToAnswered(s)

    assert.ok(s.lines.length > 0, 'should have captured at least one stdout line')
    for (const line of s.lines) {
      let msg
      assert.doesNotThrow(() => {
        msg = JSON.parse(line)
      }, `stdout line is not valid JSON: ${line}`)
      assert.equal(msg.jsonrpc, '2.0', `stdout line missing jsonrpc "2.0": ${line}`)
    }
  } finally {
    s.close()
  }
})
}
