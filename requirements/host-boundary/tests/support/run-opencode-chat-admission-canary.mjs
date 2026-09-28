import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../../..')
const pluginPath = path.join(here, 'opencode-chat-admission-canary-plugin.mjs')
const execute = promisify(execFile)

export async function runChatAdmissionCanary(verify) {
  const pluginVersion = JSON.parse(fs.readFileSync(path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json'), 'utf8')).version
  const { stdout } = await execute(OPENCODE_BIN, ['--version'], { encoding: 'utf8' })
  const versions = { opencode: stdout.trim().replace(/^v/, ''), plugin: pluginVersion }
  await verify.versions(versions)
  const observations = []
  const waiters = []
  const host = new ProcessHost()
  let provider
  let scenarioDir
  let sessionID
  let evidence
  const failures = []
  const acceptedMessageID = 'msg_chat_canary_accepted'
  const rejectedMessageID = 'msg_chat_canary_rejected'
  const normalize = (value) => {
    if (Array.isArray(value)) return value.map(normalize)
    if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([key, child]) => [key, normalize(child)]))
    if (value === sessionID) return '$session'
    if (value === acceptedMessageID) return '$accepted-message'
    if (value === rejectedMessageID) return '$rejected-message'
    return value
  }
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
  try {
    await new Promise((resolve, reject) => {
      collector.once('error', reject)
      collector.listen(0, '127.0.0.1', resolve)
    })
    const collectorUrl = `http://127.0.0.1:${collector.address().port}`

    const providerRequests = []
    provider = await startHttpServer(async (request, response) => {
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
      agent: 'engineer',
      model: { providerID: 'test', modelID: 'test-model' },
      parts: [{ type: 'text', text }],
    })

    scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-chat-admission-canary-'))
    const workspace = path.join(scenarioDir, 'workspace')
    fs.mkdirSync(workspace, { recursive: true })
    await initGitWorkspace(workspace)

    await host.start({
      scenarioDir,
      providerUrl: `${provider.url}/v1`,
      pluginPaths: [pluginPath],
      extraEnv: { WANXIANGSHU_CHAT_CANARY_COLLECTOR: collectorUrl },
      detached: false,
    })

    sessionID = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
      agent: 'engineer',
      model: { providerID: 'test', id: 'test-model' },
    }, 200))
    assert.ok(sessionID, 'public session create response omitted its id')
    await verify.ready({
      sessionID,
      pid: host.pid,
      health: await request(host.baseUrl, 'GET', '/global/health', undefined, 200),
    })

    await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(acceptedMessageID, 'CANARY_ACCEPT'), 204)
    await waitFor(({ kind }) => kind === 'provider')
    await verify.provider(normalize(observations))
    await waitFor(({ kind, value }) => kind === 'message.updated'
      && value.info.role === 'assistant'
      && value.info.parentID === acceptedMessageID
      && value.info.id)
    await waitFor(({ kind, value }) => kind === 'message.updated'
      && value.info.role === 'assistant'
      && value.info.parentID === acceptedMessageID
      && value.info.completed !== null)
    await waitFor(({ kind, value }) => kind === 'session.idle' && value.properties.sessionID === '<string>')
    await verify.terminal(normalize(observations))
    const providerDeliveriesBeforeDuplicate = providerRequests.length
    const transformsBeforeDuplicate = observations.filter(({ kind }) => kind === 'experimental.chat.messages.transform').length
    const acceptedSequence = observations.find(({ kind, value }) => kind === 'chat.message'
      && value.input.messageID === acceptedMessageID).sequence

    const duplicateResponse = await request(
      host.baseUrl,
      'POST',
      `/session/${sessionID}/prompt_async`,
      prompt(acceptedMessageID, 'CANARY_ACCEPT'),
    )
    await waitFor(({ kind, value, sequence }) => kind === 'chat.message'
      && value.input.messageID === acceptedMessageID && sequence > acceptedSequence)
    const publicStatuses = await request(host.baseUrl, 'GET', '/session/status', undefined, 200)
    const providerDeliveriesAfterDuplicate = providerRequests.length
    const transformsAfterDuplicate = observations.filter(({ kind }) => kind === 'experimental.chat.messages.transform').length
    const duplicate = {
      responseStatus: duplicateResponse.status,
      providerDeliveriesBefore: providerDeliveriesBeforeDuplicate,
      providerDeliveriesAfter: providerDeliveriesAfterDuplicate,
      transformsBefore: transformsBeforeDuplicate,
      transformsAfter: transformsAfterDuplicate,
      publicSessionStatus: publicStatuses.data?.[sessionID]?.type ?? publicStatuses.data?.data?.[sessionID]?.type ?? null,
    }
    await verify.duplicate({ ...duplicate, observations: normalize(observations) })

    const rejectedResponse = await request(
      host.baseUrl,
      'POST',
      `/session/${sessionID}/prompt_async`,
      prompt(rejectedMessageID, 'CANARY_REJECT'),
    )
    await waitFor(({ kind }) => kind === 'chat.message.rejection')
    await waitFor(({ kind }) => kind === 'session.error')
    await verify.rejection({ responseStatus: rejectedResponse.status, observations: normalize(observations) })

    evidence = normalize({
      schemaVersion: 1,
      launched: `${OPENCODE_BIN} serve --port 0 --hostname 127.0.0.1`,
      versions,
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
      duplicate,
      rejection: { responseStatus: rejectedResponse.status },
    })
  } catch (error) {
    failures.push(error)
  } finally {
    try { await host.stop() } catch (error) { failures.push(error) }
    for (const result of await Promise.allSettled([stopHttpServer(provider?.server), stopHttpServer(collector)])) {
      if (result.status === 'rejected') failures.push(result.reason)
    }
    try {
      if (scenarioDir) fs.rmSync(scenarioDir, { recursive: true, force: true })
    } catch (error) { failures.push(error) }
  }
  if (failures.length > 1) throw new AggregateError(failures, 'Chat admission canary and cleanup failed', { cause: failures[0] })
  if (failures.length === 1) throw failures[0]
  return evidence
}
