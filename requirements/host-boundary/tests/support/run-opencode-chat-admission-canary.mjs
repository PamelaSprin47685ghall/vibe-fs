import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import {
  HOST_START_TIMEOUT_MS, UNIT_VERDICT_SILENCE_MS,
  SIGTERM_GRACE_MS, SIGKILL_GRACE_MS, SOCKET_CHECK_TIMEOUT_MS, PROCESS_TREE_TIMEOUT_MS,
} from '../../../verification-system/tests/e2e/support/time-budget.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../../..')
const pluginPath = path.join(here, 'opencode-chat-admission-canary-plugin.mjs')
export const CANARY_PHYSICAL_LIMIT_MS = 30000
const HOST_RECLAIM_COMMAND_MS = 2000
const HOST_CLEANUP_RESERVE_MS = SIGTERM_GRACE_MS + SIGKILL_GRACE_MS
  + 3 * SOCKET_CHECK_TIMEOUT_MS + PROCESS_TREE_TIMEOUT_MS + HOST_RECLAIM_COMMAND_MS
const PROTOCOL_RESERVE_MS = 2000
// The old scenario was unbounded. Reserve the known cleanup stages without widening them.
// Shared process inspection has unbounded native calls, so this is not a complete cleanup proof.
const SCENARIO_LIMIT_MS = CANARY_PHYSICAL_LIMIT_MS - HOST_CLEANUP_RESERVE_MS - PROTOCOL_RESERVE_MS

const observations = []
const waiters = []
const publish = (observation) => {
  const recorded = { sequence: observations.length + 1, ...observation }
  observations.push(recorded)
  for (let index = waiters.length - 1; index >= 0; index -= 1) {
    if (!waiters[index].predicate(recorded)) continue
    waiters.splice(index, 1)[0].resolve(recorded)
  }
}
export const waitFor = (predicate, { signal = AbortSignal.timeout(UNIT_VERDICT_SILENCE_MS), label = 'public event' } = {}) => {
  const missing = () => new Error(`Missing canary observation: ${label}`, { cause: signal.reason })
  if (signal.aborted) throw missing()
  const existing = observations.find(predicate)
  if (existing) return Promise.resolve(existing)
  return new Promise((resolve, reject) => {
    const finish = (observation) => {
      signal.removeEventListener('abort', aborted)
      resolve(observation)
    }
    const waiter = { predicate, resolve: finish }
    const aborted = () => {
      const index = waiters.indexOf(waiter)
      if (index >= 0) waiters.splice(index, 1)
      signal.removeEventListener('abort', aborted)
      reject(missing())
    }
    waiters.push(waiter)
    signal.addEventListener('abort', aborted, { once: true })
  })
}

async function runCanary() {
const scenarioSignal = AbortSignal.timeout(SCENARIO_LIMIT_MS)
const pluginVersion = JSON.parse(fs.readFileSync(path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json'), 'utf8')).version
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8', timeout: HOST_START_TIMEOUT_MS }).trim().replace(/^v/, '')
scenarioSignal.throwIfAborted()
const causalSignal = () => AbortSignal.any([scenarioSignal, AbortSignal.timeout(UNIT_VERDICT_SILENCE_MS)])
const waitForEvent = (label, predicate) => waitFor(predicate, { label, signal: causalSignal() })
const collector = http.createServer(async (request, response) => {
  const chunks = []
  for await (const chunk of request) chunks.push(chunk)
  publish(JSON.parse(Buffer.concat(chunks).toString('utf8')))
  response.writeHead(204).end()
})
await new Promise((resolve, reject) => {
  collector.once('error', reject)
  collector.listen(0, '127.0.0.1', resolve)
})
const collectorUrl = `http://127.0.0.1:${collector.address().port}`

const providerRequests = []
const provider = await startHttpServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`)
  if ((url.pathname === '/v1/models' || url.pathname === '/models') && request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }
  if (url.pathname === '/v1/chat/completions' && request.method === 'POST') {
    const body = await readRequestBody(request)
    publish({
      kind: 'provider',
      value: { model: body.model, precedingHooks: observations.map(({ kind }) => kind) },
    })
    providerRequests.push(body)
    sendSSE(response, buildTextChunks(`chat_canary_${providerRequests.length}`, 'CANARY_OK', 1))
    return
  }
  sendJSON(response, 404, { error: `unexpected ${request.method} ${url.pathname}` })
})

const request = async (baseUrl, method, pathname, body, expectedStatus, signal = causalSignal()) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })
  const text = await response.text()
  if (expectedStatus !== undefined) assert.equal(response.status, expectedStatus, `${method} ${pathname}: ${text}`)
  return { status: response.status, data: text ? JSON.parse(text) : null }
}
const sessionIdOf = ({ data }) => data?.data?.data?.id ?? data?.data?.id ?? data?.id
const prompt = (messageID, text) => ({
  messageID,
  agent: 'engineer',
  model: { providerID: 'test', modelID: 'test-model' },
  parts: [{ type: 'text', text }],
})

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-chat-admission-canary-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace, { recursive: true })
await initGitWorkspace(workspace)

const host = new ProcessHost()
let stopping
const stop = () => stopping ??= host.stop()
const cancelled = () => { void stop().catch(() => {}) }
scenarioSignal.addEventListener('abort', cancelled, { once: true })
let sessionID
try {
  scenarioSignal.throwIfAborted()
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [pluginPath],
    extraEnv: { WANXIANGSHU_CHAT_CANARY_COLLECTOR: collectorUrl },
  })
  scenarioSignal.throwIfAborted()

  sessionID = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
    agent: 'engineer',
    model: { providerID: 'test', id: 'test-model' },
  }, 200))
  assert.ok(sessionID, 'public session create response omitted its id')

  const acceptedMessageID = 'msg_chat_canary_accepted'
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(acceptedMessageID, 'CANARY_ACCEPT'), 204)
  await waitForEvent('provider start', ({ kind }) => kind === 'provider')
  await waitForEvent('accepted assistant message', ({ kind, value }) => kind === 'message.updated'
    && value.info.role === 'assistant'
    && value.info.parentID === acceptedMessageID
    && value.info.id)
  await waitForEvent('accepted session idle', ({ kind, value }) => kind === 'session.idle' && value.properties.sessionID === '<string>')
  const providerDeliveriesBeforeDuplicate = providerRequests.length
  const transformsBeforeDuplicate = observations.filter(({ kind }) => kind === 'experimental.chat.messages.transform').length

  const duplicateResponse = await request(
    host.baseUrl,
    'POST',
    `/session/${sessionID}/prompt_async`,
    prompt(acceptedMessageID, 'CANARY_ACCEPT'),
  )
  const publicStatuses = await request(host.baseUrl, 'GET', '/session/status', undefined, 200)
  const providerDeliveriesAfterDuplicate = providerRequests.length
  const transformsAfterDuplicate = observations.filter(({ kind }) => kind === 'experimental.chat.messages.transform').length

  const rejectedMessageID = 'msg_chat_canary_rejected'
  const rejectedResponse = await request(
    host.baseUrl,
    'POST',
    `/session/${sessionID}/prompt_async`,
    prompt(rejectedMessageID, 'CANARY_REJECT'),
  )
  await waitForEvent('chat.message rejection', ({ kind }) => kind === 'chat.message.rejection')
  await waitForEvent('rejected session error', ({ kind }) => kind === 'session.error')

  const normalize = (value) => {
    if (Array.isArray(value)) return value.map(normalize)
    if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([key, child]) => [key, normalize(child)]))
    if (value === sessionID) return '$session'
    if (value === acceptedMessageID) return '$accepted-message'
    if (value === rejectedMessageID) return '$rejected-message'
    return value
  }

  process.stdout.write(`${JSON.stringify(normalize({
    schemaVersion: 1,
    launched: `${OPENCODE_BIN} serve --port 0 --hostname 127.0.0.1`,
    versions: { opencode: opencodeVersion, plugin: pluginVersion },
    publicApis: {
      hooks: ['chat.message', 'chat.params', 'experimental.chat.messages.transform', 'event'],
      sdk: ['POST /api/session', 'POST /session/{id}/prompt_async', 'GET /session/status'],
    },
    observations,
    providerLifecycle: {
      chatParamsDeliveries: observations.filter(({ kind }) => kind === 'chat.params').length,
      providerDeliveries: providerRequests.length,
      assistantMessageUpdates: observations.filter(({ kind, value }) => kind === 'message.updated'
        && value.info?.role === 'assistant').length,
    },
    duplicate: {
      responseStatus: duplicateResponse.status,
      providerDeliveriesBefore: providerDeliveriesBeforeDuplicate,
      providerDeliveriesAfter: providerDeliveriesAfterDuplicate,
      transformsBefore: transformsBeforeDuplicate,
      transformsAfter: transformsAfterDuplicate,
      publicSessionStatus: publicStatuses.data?.[sessionID]?.type ?? publicStatuses.data?.data?.[sessionID]?.type ?? null,
    },
    rejection: { responseStatus: rejectedResponse.status },
  }), null, 2)}\n`)
} finally {
  scenarioSignal.removeEventListener('abort', cancelled)
  try {
    await stop()
  } finally {
    try {
      await stopHttpServer(provider.server)
    } finally {
      try { await stopHttpServer(collector) }
      finally { fs.rmSync(scenarioDir, { recursive: true, force: true }) }
    }
  }
}
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) await runCanary()
