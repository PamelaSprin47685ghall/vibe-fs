import test from 'node:test'

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { randomUUID } = await import("node:crypto");
const { readFile } = await import("node:fs/promises");
const { fixture, readJson, sha256File } = await import("./gec-support.mjs");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const FROZEN_HANDLE = 'b04716f1-811b-4869-90ae-5bc055d81a48'
const LEGACY_TOOLS = ['start', 'assess', 'propose', 'investigate', 'synthesize']
const sphinxToolPrefix = 'xd://mcp__sphinx_'
function sphinxToolOf(path) {
  return path.replace(sphinxToolPrefix, '')
}
async function loadTranscript() {
  const file = fixture('legacy', 'programming-quality.full.jsonl')
  const lines = (await readFile(file, 'utf8')).split('\n').filter((line) => line.length > 0).map(JSON.parse)

  const calls = []
  for (const line of lines) {
    if (line.type !== 'message' || line.message?.role !== 'assistant') continue
    for (const item of line.message.content ?? []) {
      if (item.type !== 'toolCall' || item.name !== 'write') continue
      if (!item.arguments?.path?.startsWith(sphinxToolPrefix)) continue
      calls.push({
        id: item.id,
        tool: sphinxToolOf(item.arguments.path),
        args: JSON.parse(item.arguments.content),
      })
    }
  }

  const outcomes = new Map()
  for (const line of lines) {
    if (line.type !== 'message' || line.message?.role !== 'toolResult') continue
    if (line.message?.toolName !== 'write') continue
    const text = (line.message.content ?? []).map((part) => part.text ?? '').join('\n')
    outcomes.set(line.message.toolCallId, text)
  }
  return { file, calls, outcomes }
}

test('WHAT[epistemic-reasoning-030] frozen_transcript_sha_and_projection_match_before_replay', async () => {
  const { file, calls, outcomes } = await loadTranscript()
  const projection = await readJson(fixture('legacy', 'programming-quality.event-projection.json'))
  const summary = await readJson(fixture('legacy', 'programming-quality.expected-summary.json'))

  // The frozen bytes are content-addressed by both fixtures.
  assert.equal(await sha256File(file), projection.sourceSha256)
  assert.equal(await sha256File(file), summary.sourceSha256)
  assert.equal(projection.records, 253)
  assert.equal(projection.invalidSubmissions, 1)
  assert.equal(projection.acceptedRevisionCount, 58)
  assert.deepEqual(projection.acceptedRevisionRange, [0, 57])
  assert.equal(projection.contiguous, true)

  // 59 sphinx writes: every legacy phase tool only, one historical failure.
  assert.equal(calls.length, 59)
  for (const call of calls) {
    assert.ok(LEGACY_TOOLS.includes(call.tool), `unexpected legacy tool ${call.tool}`)
  }
  const failed = calls.filter((call) => outcomes.get(call.id)?.startsWith('Error:'))
  assert.equal(failed.length, 1)
  assert.equal(failed[0].tool, 'investigate')
  assert.match(outcomes.get(failed[0].id), /INVALID_OBSERVATION/)
  assert.equal(typeof failed[0].args.evidence[0].source, 'string')
})
test('WHAT[epistemic-reasoning-030] fifty_eight_accepted_calls_replay_to_identical_revision_tool_sequence_with_golden_anchors', async () => {
  const { calls, outcomes } = await loadTranscript()
  const projection = await readJson(fixture('legacy', 'programming-quality.event-projection.json'))
  const summary = await readJson(fixture('legacy', 'programming-quality.expected-summary.json'))

  // Fresh inquiry: the frozen handle never leaks into the replay.
  const started = await gecSurface.replayLegacyCall({
    tool: 'start',
    args: { question: summary.question },
  })
  assert.equal(started.error, undefined)
  const inquiryId = started.inquiryId
  assert.ok(typeof inquiryId === 'string' && inquiryId.length > 0)
  assert.notEqual(inquiryId, FROZEN_HANDLE)

  const observed = [
    {
      revision: started.revision,
      nextTool: started.nextTool,
      status: started.status,
      requestType: started.request?.type ?? null,
    },
  ]
  let accepted = 1
  let terminalAnswer = null

  for (const call of calls.slice(1)) {
    const failedHistorically = outcomes.get(call.id)?.startsWith('Error:')
    const args = { ...call.args, handle: inquiryId }
    const result = await gecSurface.replayLegacyCall({ inquiryId, tool: call.tool, args })

    if (failedHistorically) {
      // The single bare-string-source submission must fail the same way
      // without advancing the revision.
      assert.ok(result.error, 'historical invalid submission must still be rejected')
      assert.match(result.error.code, /INVALID_OBSERVATION/)
      assert.equal(result.revision, observed[observed.length - 1].revision)
      continue
    }

    assert.equal(result.error, undefined, `replay of ${call.tool} must be accepted`)
    accepted += 1
    if (result.revision === 57) terminalAnswer = result.answer
    observed.push({
      revision: result.revision,
      nextTool: result.nextTool,
      status: result.status,
      requestType: result.request?.type ?? null,
    })

    // Revision-2 anchor: the kernel-selected ExperimentDesign value 1.089.
    if (result.revision === 2) {
      assert.equal(result.request?.action?.method, 'ExperimentDesign')
      assert.ok(Math.abs(result.request.action.value - 1.089) < 1e-12)
      assert.equal(result.request.action.id, summary.firstSelectedAction.id)
    }
  }

  assert.equal(accepted, 58)

  // Full request/nextTool order and per-observation revisions are identical.
  assert.deepEqual(
    observed.map(({ revision, nextTool, status, requestType }) => ({ revision, nextTool, status, requestType })),
    projection.trace,
  )

  // Revision-56 anchor: the frozen synthesis organizes the frozen keys and
  // the adapter accepts them into the terminal step.
  const preSynthesis = observed.find((entry) => entry.revision === 56)
  assert.equal(preSynthesis.nextTool, 'synthesize')
  const synthesizeCall = calls.find(
    (call) => call.tool === 'synthesize' && !outcomes.get(call.id)?.startsWith('Error:'),
  )
  assert.deepEqual(synthesizeCall.args.findingKeys, summary.synthesis.findingKeys)

  // Revision-57 anchor: terminal answer with stop-dominates.
  const final = observed[observed.length - 1]
  assert.equal(final.revision, 57)
  assert.equal(final.status, 'answered')
  assert.equal(final.nextTool, null)
  assert.ok(terminalAnswer, 'terminal step must carry the canonical answer')
  assert.equal(terminalAnswer.stopReason, summary.stopReason)
  assert.equal(terminalAnswer.stopReason, 'stop-dominates')
  assert.equal(terminalAnswer.revision, 57)

  // Terminal stability: nothing rewrites history after the answer.
  const afterTerminal = await gecSurface.replayLegacyCall({
    inquiryId,
    tool: 'synthesize',
    args: { handle: inquiryId, text: 'late rewrite', findingKeys: [], uncertainties: [] },
  })
  assert.ok(afterTerminal.error, 'submitting after the terminal answer must fail, not rewrite history')
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

test('WHAT[epistemic-reasoning-030] restart_recovers_same_durable_inquiry', { timeout: 30000 }, async () => {
  // Both servers share one durable commonDir via SPHINX_COMMON_DIR. Killing
  // the first process must not invalidate the handle: the second server
  // recovers the same durable inquiry at the same revision.
  const commonDir = await mkdtemp(join(tmpdir(), 'sphinx-mcp-'))
  try {
    const env = { SPHINX_COMMON_DIR: commonDir }
    const s1 = spawnSphinx(env)
    let handle
    try {
      await s1.initialize()
      const startRes = await s1.tool('start', { question: '花儿为什么这样红？' })
      handle = startRes.result.structuredContent.handle
      const assessed = await s1.tool('assess', assessArgs(handle))
      assert.equal(assessed.result.structuredContent.revision, 1)
    } finally {
      s1.close()
    }

    const s2 = spawnSphinx(env)
    try {
      await s2.initialize()
      const res = await s2.tool('status', { handle })
      assert.equal(res.result.isError, undefined)
      assert.equal(res.result.structuredContent.handle, handle)
      assert.equal(res.result.structuredContent.status, 'active')
      assert.equal(res.result.structuredContent.revision, 1)
      assert.equal(res.result.structuredContent.nextTool, 'propose')
    } finally {
      s2.close()
    }
  } finally {
    await rm(commonDir, { recursive: true, force: true })
  }
})
}
