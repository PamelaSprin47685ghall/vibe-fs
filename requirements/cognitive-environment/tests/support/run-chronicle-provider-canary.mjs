import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { EventProbe } from '../../../verification-system/tests/e2e/support/event-probe.js'
import { initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'
import { countFactCase } from '../../../verification-system/tests/e2e/support/journal-observer.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../../..')
// The negative leg drives the same hook path with the retired v1 `tip` argument
// instead of the v2 tip fields, so the canary proves both halves of the contract:
// v2 is accepted and committed, v1 is rejected as missing-tip-name.
const legacyTip = process.argv.includes('--legacy-tip')
const marker = fs.readFileSync(path.join(root, 'resources/provider/cognitive-environment/blogger-chronicle-text/en.md'), 'utf8').trim()
const bloggerRequests = []
// 015 diagnostics: every POST body's tool census, independent of the chronicle
// counter below. Observational only; it never feeds a pass/fail decision.
const providerRequestsCensus = []
const providerErrors = []
const callbacks = new Set()
let finish
let fail
const observed = new Promise((resolve, reject) => { finish = resolve; fail = reject })
observed.catch(() => {})
const handleProviderRequest = async (request, response) => {
  try {
    const chunks = []
    for await (const chunk of request) chunks.push(chunk)
    const bytes = Buffer.concat(chunks)
    if (request.method !== 'POST') {
      sendJSON(response, 200, { object: 'list', data: [] })
      return
    }
    const body = JSON.parse(bytes.toString('utf8'))
    // 015 diagnostics: record every POST that reached the provider, before any
    // business branch. The chronicle counter and all assertions stay unchanged.
    providerRequestsCensus.push({
      session: request.headers['x-session-affinity'] ?? null,
      model: body.model ?? null,
      tools: (body.tools ?? []).map(tool => tool.function?.name ?? tool.name),
    })
    const chronicle = (body.tools ?? []).find(tool => (tool.function?.name ?? tool.name) === 'chronicle')
    if (!chronicle) {
      sendSSE(response, buildTextChunks('main-canary', 'The material is ready for its permanent record.', 1))
      return
    }
    bloggerRequests.push({ bytes, body, session: request.headers['x-session-affinity'] })
    const hints = body.messages.filter(message => message.role === 'assistant' && message.content === marker)
    assert.equal(hints.length, 1, 'each real Blogger provider request must carry exactly one ephemeral marker')
    assert.equal(body.model, 'step-3.5-flash-canary')
    assert.ok(bytes.includes(Buffer.from(JSON.stringify(marker).slice(1, -1))), 'the actual request bytes must contain the rendered resource')
    // The chronicle call is the FIRST Blogger response so the second Host
    // request is the provider step that judges it: the enforcer decodes the
    // persisted assistant part of the previous step before answering, so only
    // this ordering observes the wire the enforcer actually reads (v2 fields
    // must survive Host persistence; a v1 `tip` call is rejected here with
    // enforcer-blog-call-invalid and never commits a cycle).
    if (bloggerRequests.length === 1) {
      const chronicleArgs = legacyTip ? {
        charge: 'Preserve the exact provider delivery evidence.',
        occurrence: 'The installed Host delivered one temporary Blogger prompt per request.',
        settlement: 'The prompt remains outside durable Host history.',
        consequence: 'A later Blogger request receives only its own temporary prompt.',
        tip: 'primitive-obsession',
      } : {
        charge: 'Preserve the exact provider delivery evidence.',
        occurrence: 'The installed Host delivered one temporary Blogger prompt per request.',
        settlement: 'The prompt remains outside durable Host history.',
        consequence: 'A later Blogger request receives only its own temporary prompt.',
        tipName: 'chronicle-provider-evidence',
        opinion: 'The installed Host should deliver one temporary Blogger prompt per request.',
        advice: 'Ensure the prompt remains outside durable Host history.',
      }
      sendSSE(response, buildToolCallChunks('chronicle-provider-canary', 'chronicle', JSON.stringify(chronicleArgs), 1))
      return
    }
    sendSSE(response, buildTextChunks('blogger-first', 'The record is complete.', 1))
    finish()
  } catch (error) {
    providerErrors.push(error)
    fail(error)
    sendJSON(response, 500, { error: error.message })
  }
}

const host = new ProcessHost()
let provider
let scenarioDir
let timeout
let probe
let result
// 015 diagnostics: the manager session id, hoisted so the failure path can ask
// the Host for its children. Assignment semantics are unchanged.
let sessionId
const failures = []
const cleanupErrors = []
const api = async (pathname, body) => {
  const response = await fetch(host.baseUrl + pathname, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  assert.ok(response.ok, `${pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const valueOf = response => response?.data?.data ?? response?.data ?? response
// 015 diagnostics: best-effort failure observation. Every probe is wrapped so a
// diagnostic failure can never become a canary failure; nothing here feeds any
// assertion, status code or canary timing decision (the verdict is already
// captured in `failures` before this runs).
const collectFailureDiagnostics = async () => {
  const note = message => console.error(`[015-diagnostics] ${message}`)
  const logTail = (label, file) => {
    try {
      if (!file || !fs.existsSync(file)) return
      const lines = fs.readFileSync(file, 'utf8').split('\n')
      note(`${label} tail (${lines.length} lines):\n${lines.slice(-120).join('\n')}`)
    } catch (error) {
      note(`${label} unavailable: ${error?.message ?? error}`)
    }
  }
  try {
    logTail('opencode.log', path.join(host.workDir, 'opencode.log'))
    logTail('scenario opencode.log', path.join(scenarioDir ?? '', 'opencode.log'))
  } catch (error) {
    note(`opencode.log collection skipped: ${error?.message ?? error}`)
  }
  if (sessionId) {
    try {
      const response = await fetch(`${host.baseUrl}/session/${sessionId}/children`, {
        signal: AbortSignal.timeout(3000),
      })
      const text = await response.text()
      note(`children(${sessionId}) status=${response.status} body=${text.slice(0, 4000)}`)
    } catch (error) {
      note(`children observation skipped: ${error?.message ?? error}`)
    }
  } else {
    note('children observation skipped: manager session id was never assigned')
  }
  note(`provider requests total=${providerRequestsCensus.length}; chronicle=${bloggerRequests.length}`)
  for (const [index, request] of providerRequestsCensus.entries()) {
    note(`request ${index} session=${request.session} model=${request.model} tools=${request.tools.join(',') || '(none)'}`)
  }
}
try {
  timeout = setTimeout(() => fail(new Error(`Blogger provider requests missing: observed ${bloggerRequests.length}`)), 30000)
  provider = await startHttpServer((request, response) => {
    const callback = handleProviderRequest(request, response).catch(error => {
      providerErrors.push(error)
      fail(error)
    })
    callbacks.add(callback)
    callback.then(() => callbacks.delete(callback))
  })
  scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-chronicle-provider-'))
  fs.mkdirSync(path.join(scenarioDir, 'workspace'))
  await initGitWorkspace(path.join(scenarioDir, 'workspace'))
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [path.join(here, 'chronicle-provider-canary-plugin.mjs')],
    routingSource: `export const routingProtocol = 2
export const hasTheoreticalCapacity = () => true
export const predictorConfiguration = () => ({ state: 'unconfigured', reason: null })
export default function route(role) { return { model: role === 'blogger' ? 'test/step-3.5-flash-canary' : 'test/test-model', reasoning: 'none' } }
`,
    extraEnv: { WXS_CHRONICLE_PRODUCTION_PLUGIN: path.join(root, 'dist/OpenCode/Plugin/Plugin.js') },
  })
  probe = new EventProbe(host.baseUrl, host.workDir)
  await probe.connect()
  sessionId = valueOf(await api('/api/session', { agent: 'manager', model: { providerID: 'test', id: 'test-model' } })).id
  const session = sessionId
  await api(`/session/${session}/prompt_async`, {
    messageID: 'msg-chronicle-provider-canary', agent: 'manager',
    model: { providerID: 'test', modelID: 'test-model' },
    parts: [{ type: 'text', text: 'Record the delivery of a temporary Blogger hint.' }],
  })
  await observed
  const childSessions = valueOf(await api(`/session/${session}/children`))
  assert.ok(Array.isArray(childSessions), 'the Host child session response must be a concrete array')
  assert.equal(childSessions.length, 1, 'both provider requests must belong to the single actual companion Blogger')
  const terminal = await probe.awaitEvent(event => {
    const part = event.properties?.part
    return event.type === 'message.part.updated'
      && part?.sessionID === childSessions[0].id
      && part?.type === 'tool' && part.callID === 'chronicle-provider-canary'
      && ['completed', 'error'].includes(part.state?.status)
  }, 10000)
  assert.equal(terminal.properties.part.state.status, 'completed', 'the actual chronicle tool must complete before final history is accepted')
  for (const child of childSessions) {
    const history = valueOf(await api(`/session/${child.id}/message`))
    const chroniclePart = history.flatMap(message => message.parts)
      .find(part => part.type === 'tool' && part.callID === 'chronicle-provider-canary')
    assert.ok(chroniclePart, 'the declared chronicle call must complete before history is accepted')
    assert.equal(chroniclePart.state?.status, 'completed', 'the declared chronicle call must complete before history is accepted')
    // The enforcer reads the persisted tool part of the previous step, so the
    // provider-declared arguments must survive Host persistence verbatim. Only
    // the v2 call carries the fields the codec requires; the v1 leg persists the
    // retired `tip` alone, which is what the enforcer must reject below.
    if (legacyTip) {
      assert.equal(chroniclePart.state?.input?.tipName, undefined,
        'the v1 call must persist no v2 tipName; the codec has only the retired tip argument')
    } else {
      assert.equal(chroniclePart.state?.input?.tipName, 'chronicle-provider-evidence',
        'the Host must persist the provider-declared v2 tipName, not a stripped argument set')
      assert.ok(chroniclePart.state?.input?.opinion && chroniclePart.state?.input?.advice,
        'the Host must persist the provider-declared v2 opinion and advice')
    }
    assert.equal(JSON.stringify(history).includes(marker), false, 'the real Host persisted history must exclude the injected hint')
  }
  const events = path.join(host.workDir, '.Herdchestra/wanxiangshu/events')
  const walk = directory => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const filename = path.join(directory, entry.name)
      if (entry.isDirectory()) walk(filename)
      else assert.equal(fs.readFileSync(filename, 'utf8').includes(marker), false, 'durable journal must exclude the provider-only hint')
    }
  }
  walk(events)
  // The second Blogger request transformed the previous step, so the enforcer
  // judged the chronicle call there: acceptance is one durable cycle, rejection
  // is the named diagnostic and no cycle (ENFORCER-023/060).
  const committedCycles = countFactCase(host.workDir, 'BlogObservationCommitted')
  if (legacyTip) {
    assert.equal(committedCycles, 0,
      'the enforcer must reject the retired v1 tip call and commit no cycle')
    assert.match(host.stderrLog, /enforcer-blog-call-invalid[\s\S]*missing-tip-name/,
      'the v1 rejection must surface as the enforcer blog-call-invalid diagnostic naming missing-tip-name')
  } else {
    assert.equal(committedCycles, 1,
      'the enforcer must accept the provider-declared v2 chronicle call and commit exactly one cycle')
  }
  if (process.argv.includes('--late-provider-error')) {
    const response = await fetch(`${provider.url}/v1/chat/completions`, { method: 'POST', body: '{' })
    assert.equal(response.status, 500, 'the real late HTTP request must reach the provider callback error path')
    await response.text()
  }
  result = legacyTip
    ? { legacyTipRejected: true }
    : { providerRequests: bloggerRequests.length, chronicleCompleted: true, historyClean: true, journalClean: true }
} catch (error) {
  console.error(host.stdoutLog)
  await collectFailureDiagnostics()
  failures.push(error)
} finally {
  clearTimeout(timeout)
  for (const cleanup of [() => probe?.close(), () => host.stop(), () => provider && stopHttpServer(provider.server),
    () => Promise.all(callbacks), () => scenarioDir && fs.rmSync(scenarioDir, { recursive: true, force: true })]) {
    try { await cleanup() } catch (error) { cleanupErrors.push(error) }
  }
}
const errors = [...new Set([...failures, ...providerErrors, ...cleanupErrors])]
if (errors.length === 1) throw errors[0]
if (errors.length > 1) throw new AggregateError(errors, 'Provider canary failed', { cause: errors[0] })
assert.equal(bloggerRequests.length, 2, 'shutdown must not conceal extra Blogger requests')
console.log(JSON.stringify(result))
