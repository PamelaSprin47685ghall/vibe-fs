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

test('WHAT[epistemic-reasoning-002] fsharp_kernel_has_no_agent_host_domain_dependency_and_sdk_stays_at_mcp_edge', () => {
  const sourceDir = join(root, 'src/Wanxiangshu/Sphinx')
  const files = readdirSync(sourceDir).filter((name) => name.endsWith('.fs')).sort()
  assert.ok(files.length >= 10)

  for (const name of files) {
    const source = readFileSync(join(sourceDir, name), 'utf8')
    assert.doesNotMatch(source, /open Wanxiangshu\.(Domain|OpenCode|Journal|Session|Agent|Application)/)
    if (name !== 'McpServer.fs') assert.doesNotMatch(source, /@modelcontextprotocol\/sdk|\bzod\b/)
  }

  // W5: the wrapper aggregate is gone — the compile-order manifest is the
  // authoritative production enumeration. Sphinx sources must still be
  // compiled into the canonical pipeline.
  const order = readFileSync(join(root, 'src/Wanxiangshu/compile-order.txt'), 'utf8')
  assert.match(order, /Sphinx\/Types\.fs/)
  assert.match(order, /Sphinx\/McpServer\.fs/)

  const build = readFileSync(join(root, 'scripts/build.mjs'), 'utf8')
  assert.doesNotMatch(build, /fs\.cpSync\([^\n]*sphinx/i)
  assert.ok(build.includes(relativeServerEntry))
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

test('WHAT[epistemic-reasoning-002] terminal_answered_rejects_further_observations', async () => {
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
test('WHAT[epistemic-reasoning-002] cancel_releases_handle_and_makes_it_unknown', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mcpServer } = await import("../../../dist/Sphinx/Surface.js");
const { createStore, start, resume, assessWhy } = await import("./support.mjs");


test('WHAT[epistemic-reasoning-002] handle_is_opaque_process_local_session_key', () => {
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
test('WHAT[epistemic-reasoning-002] full_co_yield_path_preserves_kernel_continuation', () => {
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

test('WHAT[epistemic-reasoning-002] interleaved_inquiries_stay_independent', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()

    const aStart = await s.tool('start', { question: '花儿为什么这样红？' })
    const A = aStart.result.structuredContent.handle
    const bStart = await s.tool('start', { question: '明天白银会涨吗？' })
    const B = bStart.result.structuredContent.handle

    // assess A, then assess B
    await s.tool('assess', assessArgs(A))
    await s.tool('assess', assessArgs(B))

    // propose B, then propose A
    const bProp = await s.tool('propose', {
      handle: B,
      items: [
        {
          method: 'CausalMechanism',
          question: '白银市场驱动因素？',
          semanticKey: 'question:silver',
          dependencyKey: 'source:silver-study',
          expectedRootGain: 0.9,
          cost: 0.2,
        },
      ],
    })
    const bActionKey = bProp.result.structuredContent.request.action.id
    assert.equal(bProp.result.structuredContent.nextTool, 'investigate')

    const aProp = await s.tool('propose', {
      handle: A,
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
    const aActionKey = aProp.result.structuredContent.request.action.id
    assert.equal(aProp.result.structuredContent.nextTool, 'investigate')

    // investigate A with A's actionKey
    const aInv = await s.tool('investigate', investigateArgs(A, aActionKey))
    assert.equal(aInv.result.structuredContent.nextTool, 'propose')

    // verify independence via status
    const aStatus = await s.tool('status', { handle: A })
    const bStatus = await s.tool('status', { handle: B })
    assert.equal(aStatus.result.structuredContent.nextTool, 'propose')
    assert.equal(bStatus.result.structuredContent.nextTool, 'investigate')
    assert.notEqual(
      aStatus.result.structuredContent.revision,
      bStatus.result.structuredContent.revision,
    )

    // B's investigate with A's actionKey → KERNEL_REJECTED
    const bBad = await s.tool('investigate', investigateArgs(B, aActionKey))
    assert.equal(bBad.result.isError, true)
    assert.equal(bBad.result.structuredContent, undefined)
    assert.equal(bBad.result._meta.error.code, 'KERNEL_REJECTED')
  } finally {
    s.close()
  }
})
test('WHAT[epistemic-reasoning-002] cancel_over_wire_then_status_unknown', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    const startRes = await s.tool('start', { question: '花儿为什么这样红？' })
    const handle = startRes.result.structuredContent.handle

    const cancelRes = await s.tool('cancel', { handle })
    assert.equal(cancelRes.result.structuredContent.status, 'cancelled')

    const statusRes = await s.tool('status', { handle })
    assert.equal(statusRes.result.isError, true)
    assert.equal(statusRes.result.structuredContent, undefined)
    assert.equal(statusRes.result._meta.error.code, 'UNKNOWN_HANDLE')
  } finally {
    s.close()
  }
})
}

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { createStore, start, resume, state, mcpServer } = await import("../../../dist/Sphinx/Surface.js");


test('WHAT[epistemic-reasoning-002] answered_returns_structured_answer_and_null_next_tool', async () => {
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
}
