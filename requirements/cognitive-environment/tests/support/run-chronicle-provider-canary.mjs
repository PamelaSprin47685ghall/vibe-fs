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

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../../..')
const marker = fs.readFileSync(path.join(root, 'resources/provider/cognitive-environment/blogger-chronicle-text/en.md'), 'utf8').trim()
const bloggerRequests = []
let finish
let fail
const observed = new Promise((resolve, reject) => { finish = resolve; fail = reject })
const timeout = setTimeout(() => fail(new Error(`Blogger provider requests missing: observed ${bloggerRequests.length}`)), 30000)
const provider = await startHttpServer(async (request, response) => {
  try {
    const chunks = []
    for await (const chunk of request) chunks.push(chunk)
    const bytes = Buffer.concat(chunks)
    if (request.method !== 'POST') {
      sendJSON(response, 200, { object: 'list', data: [] })
      return
    }
    const body = JSON.parse(bytes.toString('utf8'))
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
    if (bloggerRequests.length === 1) {
      sendSSE(response, buildTextChunks('blogger-first', 'The current record still needs its chronicle call.', 1))
      return
    }
    const tip = chronicle.function.parameters.properties.tip.enum[0]
    sendSSE(response, buildToolCallChunks('chronicle-provider-canary', 'chronicle', JSON.stringify({
      charge: 'Preserve the exact provider delivery evidence.',
      occurrence: 'The installed Host delivered one temporary Blogger prompt per request.',
      settlement: 'The prompt remains outside durable Host history.',
      consequence: 'A later Blogger request receives only its own temporary prompt.',
      tip,
    }), 1))
    finish()
  } catch (error) {
    fail(error)
    sendJSON(response, 500, { error: error.message })
  }
})

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-chronicle-provider-'))
fs.mkdirSync(path.join(scenarioDir, 'workspace'))
await initGitWorkspace(path.join(scenarioDir, 'workspace'))
const host = new ProcessHost()
let probe
let failure
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
try {
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
  const session = valueOf(await api('/api/session', { agent: 'manager', model: { providerID: 'test', id: 'test-model' } })).id
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
    assert.ok(history.some(message => message.parts.some(part => part.type === 'tool'
      && part.callID === 'chronicle-provider-canary' && part.state.status === 'completed')),
    'the second response chronicle call must actually complete before history is accepted')
    assert.equal(JSON.stringify(history).includes(marker), false, 'the real Host persisted history must exclude the injected hint')
  }
  const events = path.join(host.workDir, '.git/wanxiang/events')
  const walk = directory => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const filename = path.join(directory, entry.name)
      if (entry.isDirectory()) walk(filename)
      else assert.equal(fs.readFileSync(filename, 'utf8').includes(marker), false, 'durable journal must exclude the provider-only hint')
    }
  }
  walk(events)
  console.log(JSON.stringify({ providerRequests: bloggerRequests.length, chronicleCompleted: true, historyClean: true, journalClean: true }))
} catch (error) {
  console.error(host.stdoutLog)
  failure = error
} finally {
  clearTimeout(timeout)
  const cleanupErrors = []
  for (const cleanup of [() => probe?.close(), () => host.stop(), () => stopHttpServer(provider.server),
    () => fs.rmSync(scenarioDir, { recursive: true, force: true })]) {
    try { await cleanup() } catch (error) { cleanupErrors.push(error) }
  }
  if (failure && cleanupErrors.length > 0) {
    throw new AggregateError([failure, ...cleanupErrors], 'Provider canary and cleanup failed', { cause: failure })
  }
  if (failure) throw failure
  if (cleanupErrors.length > 0) {
    throw new AggregateError(cleanupErrors, 'Provider canary cleanup failed', { cause: cleanupErrors[0] })
  }
}
