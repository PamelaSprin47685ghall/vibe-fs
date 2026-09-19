import test from 'node:test'

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { decode, decodeSemanticAssessmentObservation, decodeCandidatesObservation, decodeInvestigationObservation, decodeSynthesisObservation } = await import("../../../dist/Sphinx/Surface.js");


test('WHAT[epistemic-reasoning-004] decode and decodeSemanticAssessmentObservation produce same result for SemanticAssessment raw', () => {
  const raw = { type: 'SemanticAssessment', forms: { Polar: 0.9, Other: 0.1 } }
  const generic = decode(raw)
  const specific = decodeSemanticAssessmentObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'SemanticAssessment')
  assert.equal(specific.observationType, 'SemanticAssessment')
})
test('WHAT[epistemic-reasoning-004] decode and decodeCandidatesObservation produce same result for Candidates raw', () => {
  const raw = {
    type: 'Candidates',
    items: [{ method: 'why', question: 'why X?', semanticKey: 'k1' }],
  }
  const generic = decode(raw)
  const specific = decodeCandidatesObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Candidates')
  assert.equal(specific.observationType, 'Candidates')
})
test('WHAT[epistemic-reasoning-004] decode and decodeInvestigationObservation produce same result for Investigation raw', () => {
  const raw = { type: 'Investigation', actionKey: 'action-1' }
  const generic = decode(raw)
  const specific = decodeInvestigationObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Investigation')
  assert.equal(specific.observationType, 'Investigation')
})
test('WHAT[epistemic-reasoning-004] decode and decodeSynthesisObservation produce same result for Synthesis raw', () => {
  const raw = { type: 'Synthesis', text: 'summary' }
  const generic = decode(raw)
  const specific = decodeSynthesisObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Synthesis')
  assert.equal(specific.observationType, 'Synthesis')
})
test('WHAT[epistemic-reasoning-004] decode rejects unknown observation type', () => {
  const raw = { type: 'Unknown' }
  const result = decode(raw)
  assert.equal(result.ok, false)
  assert.ok(result.error)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync, readdirSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { close, createStore, start, resume, state, assessWhy, relativeServerEntry } = await import("./support.mjs");

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '../../..')

test('WHAT[epistemic-reasoning-004] resume_rejects_observation_that_does_not_match_pending_kernel_request', () => {
  const store = createStore()
  const started = start(store, '为什么程序卡住？')
  const before = state(store, started.handle).Revision

  const wrong = resume(store, started.handle, {
    type: 'Synthesis',
    text: '跳过调查直接作答',
  })

  assert.equal(wrong.status, 'error')
  assert.match(wrong.error, /expected SemanticAssessment/)
  assert.equal(state(store, started.handle).Revision, before)
})
}

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

test('WHAT[epistemic-reasoning-004] wrong_phase_returns_kernel_rejected_without_advancing', async () => {
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
test('WHAT[epistemic-reasoning-004] wrong_action_key_returns_kernel_rejected_revision_unchanged', async () => {
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

test('WHAT[epistemic-reasoning-004] wrong_phase_over_wire_returns_kernel_rejected', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    const startRes = await s.tool('start', { question: QUESTION })
    const handle = startRes.result.structuredContent.handle
    const res = await s.tool('synthesize', { handle, text: 'wrong phase', findingKeys: [] })
    assert.equal(res.result.isError, true)
    assert.equal(res.result.structuredContent, undefined)
    assert.equal(res.result._meta.error.code, 'KERNEL_REJECTED')
    assert.equal(res.result._meta.error.expectedTool, 'assess')
  } finally {
    s.close()
  }
})
}

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { createStore, start, resume, state, mcpServer } = await import("../../../dist/Sphinx/Surface.js");


test('WHAT[epistemic-reasoning-004] wrong_phase_returns_typed_error_without_structured_content', async () => {
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
test('WHAT[epistemic-reasoning-004] kernel_reject_does_not_advance_revision', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { serverName, permissionKey, relativeServerEntry, isTool, localCommand, fixtureCommand } = await import("../../../dist/Sphinx/Surface.js");


test('WHAT[epistemic-reasoning-004] AGENT_030_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'sphinx')
  assert.equal(permissionKey, 'sphinx_*')
  assert.equal(relativeServerEntry, 'dist/Sphinx/ServeEntry.js')
  assert.equal(isTool('sphinx_start'), true)
  assert.equal(isTool('sphinx_resume'), true)
  assert.equal(isTool('stealth-browser-mcp_get_debug_view'), false)
  assert.equal(isTool('inspect'), false)
  assert.deepEqual(localCommand('/tmp/entry.js'), ['node', '/tmp/entry.js'])
  assert.deepEqual(fixtureCommand('/tmp/sphinx-fixture.js'), ['node', '/tmp/sphinx-fixture.js'])
})
}
