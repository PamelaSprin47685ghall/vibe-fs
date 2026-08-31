import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../../..')
const pluginPath = path.join(here, 'opencode-chat-admission-canary-plugin.mjs')
const pluginVersion = JSON.parse(fs.readFileSync(path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json'), 'utf8')).version
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim().replace(/^v/, '')

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
const waitFor = (predicate) => {
  const existing = observations.find(predicate)
  return existing ? Promise.resolve(existing) : new Promise((resolve) => waiters.push({ predicate, resolve }))
}

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

const request = async (baseUrl, method, pathname, body, expectedStatus) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  if (expectedStatus !== undefined) assert.equal(response.status, expectedStatus, `${method} ${pathname}: ${text}`)
  return { status: response.status, data: text ? JSON.parse(text) : null }
}
const sessionIdOf = ({ data }) => data?.data?.data?.id ?? data?.data?.id ?? data?.id
const prompt = (messageID, text) => ({
  messageID,
  agent: 'fast-coder',
  model: { providerID: 'test', modelID: 'test-model' },
  parts: [{ type: 'text', text }],
})

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-chat-admission-canary-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace, { recursive: true })
await initGitWorkspace(workspace)

const host = new ProcessHost()
let sessionID
try {
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [pluginPath],
    extraEnv: { WANXIANGSHU_CHAT_CANARY_COLLECTOR: collectorUrl },
  })

  sessionID = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
    agent: 'fast-coder',
    model: { providerID: 'test', id: 'test-model' },
  }, 200))
  assert.ok(sessionID, 'public session create response omitted its id')

  const acceptedMessageID = 'msg_chat_canary_accepted'
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(acceptedMessageID, 'CANARY_ACCEPT'), 204)
  await waitFor(({ kind }) => kind === 'provider')
  await waitFor(({ kind, value }) => kind === 'message.updated'
    && value.info.role === 'assistant'
    && value.info.parentID === acceptedMessageID
    && value.info.id)
  await waitFor(({ kind, value }) => kind === 'session.idle' && value.properties.sessionID === '<string>')
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
  await waitFor(({ kind }) => kind === 'chat.message.rejection')
  await waitFor(({ kind }) => kind === 'session.error')

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
  if (sessionID && host.baseUrl) {
    try { await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, 204) } catch {}
  }
  try { await host.stop() } catch {}
  await stopHttpServer(provider.server)
  await new Promise((resolve) => collector.close(resolve))
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
