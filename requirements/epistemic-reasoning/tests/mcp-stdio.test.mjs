import test from 'node:test'
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

// ─── repo paths ──────────────────────────────────────────────────────────────
const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const serverEntry = join(repoRoot, 'dist', 'Sphinx', 'McpServer.js')
const pkgVersion = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')).version

// ─── shared payloads (reused from mcp-wire-characterization.test.mjs) ─────────
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

// ─── spawn helper ────────────────────────────────────────────────────────────
/**
 * Spawns `node dist/Sphinx/McpServer.js` and returns a client that speaks
 * newline-delimited JSON-RPC 2.0 over stdio.  Every non-empty stdout line is
 * captured in `lines` for purity assertions.
 */
function spawnSphinx() {
  const proc = spawn('node', [serverEntry], {
    cwd: repoRoot,
    stdio: ['pipe', 'pipe', 'pipe'],
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

// ─── flow helper: drive one inquiry to answered, return handle ───────────────
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

// ─── tests ───────────────────────────────────────────────────────────────────

test('WHAT[EPI-014] initialize_returns_server_identity_and_instructions', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    const res = await s.initialize()
    assert.equal(res.result.serverInfo.name, 'sphinx')
    assert.equal(res.result.serverInfo.version, pkgVersion)
    assert.ok(typeof res.result.instructions === 'string' && res.result.instructions.length > 0)
    assert.match(res.result.instructions, /nextTool/)
  } finally {
    s.close()
  }
})

test('WHAT[EPI-013] tools_list_returns_eight_tools_with_schemas', { timeout: 30000 }, async () => {
  const s = spawnSphinx()
  try {
    await s.initialize()
    const res = await s.call('tools/list', {})
    const names = res.result.tools.map((t) => t.name)
    assert.deepEqual(names, [
      'start',
      'assess',
      'propose',
      'investigate',
      'synthesize',
      'status',
      'cancel',
      'resume',
    ])
    for (const t of res.result.tools) {
      assert.ok(t.inputSchema, `${t.name} must have an inputSchema`)
    }
    const resume = res.result.tools.find((t) => t.name === 'resume')
    assert.match(resume.description, /^Legacy compatibility tool/)
    const investigate = res.result.tools.find((t) => t.name === 'investigate')
    assert.ok(
      investigate.inputSchema.required?.includes('actionKey'),
      'investigate schema must require actionKey',
    )
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

test('WHAT[EPI-004] wrong_phase_over_wire_returns_kernel_rejected', { timeout: 30000 }, async () => {
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

test('WHAT[EPI-002] interleaved_inquiries_stay_independent', { timeout: 30000 }, async () => {
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

test('WHAT[EPI-002] cancel_over_wire_then_status_unknown', { timeout: 30000 }, async () => {
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

test('WHAT[EPI-013] restart_invalidates_handles', { timeout: 30000 }, async () => {
  // server 1: start → handle
  const s1 = spawnSphinx()
  let handle
  try {
    await s1.initialize()
    const startRes = await s1.tool('start', { question: '花儿为什么这样红？' })
    handle = startRes.result.structuredContent.handle
  } finally {
    s1.close()
  }

  // server 2: handle from server 1 is unknown (process-local, no persistence)
  const s2 = spawnSphinx()
  try {
    await s2.initialize()
    const res = await s2.tool('status', { handle })
    assert.equal(res.result.isError, true)
    assert.equal(res.result.structuredContent, undefined)
    assert.equal(res.result._meta.error.code, 'UNKNOWN_HANDLE')
  } finally {
    s2.close()
  }
})
