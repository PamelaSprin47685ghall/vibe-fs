// requirements/obligation-ledger/tests/support/run-native-todo-replacement-canary.mjs
//
// B5 canary for WHAT[obligation-ledger-002]: on the installed OpenCode Host,
// the native todowrite executor replaces and clears only the current
// session's TodoTable. A strict mock provider drives the real Host
// (`opencode serve`) with real todowrite tool calls while the production
// wanxiangshu plugin is loaded, and the public SDK todo endpoint
// (`GET /session/{id}/todo`) is the oracle — never a stub executor.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { resolvePluginPath } from '../../../verification-system/tests/e2e/support/scenario-paths.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../../..')
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim().replace(/^v/, '')
const pluginVersion = JSON.parse(fs.readFileSync(path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json'), 'utf8')).version

// The exact arrays the mock provider submits through real todowrite calls:
// duplicate rows, Chinese content, multiline text and explicit priorities —
// the shapes WHAT[obligation-ledger-002] forbids the plugin from rewriting.
const sessionATodos = [
  { content: '重复内容', status: 'pending', priority: 'low' },
  { content: '重复内容', status: 'in_progress', priority: 'high' },
  { content: '多行文本\n第二行\n第三行', status: 'pending', priority: 'medium' },
  { content: 'unicode ☕ 与中文混排', status: 'completed', priority: 'low' },
]
const sessionBTodos = [
  { content: 'B 独立待办', status: 'pending', priority: 'high' },
  { content: 'B 第二行', status: 'in_progress', priority: 'low' },
]

const messageText = (body) => (body?.messages ?? [])
  .flatMap((message) => {
    if (typeof message?.content === 'string') return [message.content]
    if (Array.isArray(message?.content)) return message.content.map((item) => item?.text ?? '').filter(Boolean)
    return []
  })
  .join('\n')

const hasToolResult = (body) => (body?.messages ?? []).some((message) => message?.role === 'tool')

const todoCall = (id, todos) => buildToolCallChunks(id, 'todowrite', JSON.stringify({ todos }), 1)

const providerRequests = []
const provider = await startHttpServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`)
  if ((url.pathname === '/v1/models' || url.pathname === '/models') && request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }
  if (url.pathname === '/v1/chat/completions' && request.method === 'POST') {
    const body = await readRequestBody(request)
    providerRequests.push(body)
    // A request carrying tool results means the native executor has already
    // run the todowrite call; finish the turn with plain text.
    if (hasToolResult(body)) {
      sendSSE(response, buildTextChunks(`ol002_round_${providerRequests.length}`, 'OL002_DONE', 1))
      return
    }
    const text = messageText(body)
    if (text.includes('OL002_A_CLEAR')) {
      sendSSE(response, todoCall('ol002-a-clear', []))
      return
    }
    if (text.includes('OL002_A_SET')) {
      sendSSE(response, todoCall('ol002-a-set', sessionATodos))
      return
    }
    if (text.includes('OL002_B_SET')) {
      sendSSE(response, todoCall('ol002-b-set', sessionBTodos))
      return
    }
    sendJSON(response, 500, { error: `unexpected prompt text: ${text.slice(0, 200)}` })
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

const prompt = (text) => ({
  agent: 'engineer',
  model: { providerID: 'test', modelID: 'test-model' },
  parts: [{ type: 'text', text }],
})

const fetchTodos = async (baseUrl, sessionId) => {
  const response = await fetch(`${baseUrl}/session/${sessionId}/todo`)
  const text = await response.text()
  assert.equal(response.status, 200, `GET /session/${sessionId}/todo: ${text}`)
  return JSON.parse(text)
}

// Causal wait: every poll observes the provider's actual request log. The
// wait ends exactly when the tool-result round for a marker has arrived —
// the physical proof the native executor already ran that todowrite call.
// The wall-clock deadline is only an upper bound, never the success signal.
const waitForToolResultRound = async (marker, host) => {
  const deadline = Date.now() + 30000
  const matches = () => providerRequests.some((body) => {
    const text = messageText(body)
    return text.includes(marker) && hasToolResult(body)
      && (marker !== 'OL002_A_SET' || !text.includes('OL002_A_CLEAR'))
  })
  while (Date.now() < deadline) {
    if (matches()) return
    await delay(200)
  }
  throw new Error(
    `timeout waiting for the tool-result round of ${marker}; host stderr tail:\n${host.stderrLog.slice(-4000)}`,
  )
}

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-native-todo-canary-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace, { recursive: true })
await initGitWorkspace(workspace)

const host = new ProcessHost()
let sessionA
let sessionB
try {
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [resolvePluginPath('opencode')],
  })

  sessionA = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
    agent: 'engineer',
    model: { providerID: 'test', id: 'test-model' },
  }, 200))
  sessionB = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
    agent: 'engineer',
    model: { providerID: 'test', id: 'test-model' },
  }, 200))
  assert.ok(sessionA && sessionB && sessionA !== sessionB, 'public session create responses omitted distinct ids')

  await request(host.baseUrl, 'POST', `/session/${sessionA}/prompt_async`, prompt('OL002_A_SET 为会话 A 提交完整待办列表'), 204)
  await waitForToolResultRound('OL002_A_SET', host)
  const aAfterSet = await fetchTodos(host.baseUrl, sessionA)

  await request(host.baseUrl, 'POST', `/session/${sessionB}/prompt_async`, prompt('OL002_B_SET 为会话 B 提交自己的待办列表'), 204)
  await waitForToolResultRound('OL002_B_SET', host)
  const bAfterSet = await fetchTodos(host.baseUrl, sessionB)
  const aAfterBSet = await fetchTodos(host.baseUrl, sessionA)

  await request(host.baseUrl, 'POST', `/session/${sessionA}/prompt_async`, prompt('OL002_A_CLEAR 清空会话 A 的待办'), 204)
  await waitForToolResultRound('OL002_A_CLEAR', host)
  const aAfterClear = await fetchTodos(host.baseUrl, sessionA)
  const bAfterAClear = await fetchTodos(host.baseUrl, sessionB)

  // Diagnostic fork only — never consumed by assertions. The first read above
  // is the oracle point; this settled re-read after a quiescence window
  // distinguishes "clear landed late" (asynchronous projection) from "table
  // never cleared" (executor path interrupted). hostStderrTail surfaces any
  // plugin hook exception (journal append failure, event hook crash) that the
  // swallowed tool-result path would otherwise hide.
  await delay(1000)
  const aAfterClearSettled = await fetchTodos(host.baseUrl, sessionA)

  process.stdout.write(`${JSON.stringify({
    schemaVersion: 1,
    launched: `${OPENCODE_BIN} serve --port 0 --hostname 127.0.0.1`,
    versions: { opencode: opencodeVersion, plugin: pluginVersion },
    publicSdk: ['POST /api/session', 'POST /session/{id}/prompt_async', 'GET /session/{id}/todo'],
    submitted: { sessionA: sessionATodos, sessionB: sessionBTodos, sessionAClear: [] },
    observed: {
      afterASet: { sessionA: aAfterSet },
      afterBSet: { sessionA: aAfterBSet, sessionB: bAfterSet },
      afterAClear: { sessionA: aAfterClear, sessionB: bAfterAClear },
      afterAClearSettled: { sessionA: aAfterClearSettled },
    },
    hostStderrTail: host.stderrLog.slice(-4000),
    providerRequests: providerRequests.length,
  }, null, 2)}\n`)
} finally {
  if (sessionA && host.baseUrl) {
    try { await request(host.baseUrl, 'POST', `/session/${sessionA}/abort`, {}, 204) } catch {}
  }
  if (sessionB && host.baseUrl) {
    try { await request(host.baseUrl, 'POST', `/session/${sessionB}/abort`, {}, 204) } catch {}
  }
  try { await host.stop() } catch {}
  await stopHttpServer(provider.server)
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
