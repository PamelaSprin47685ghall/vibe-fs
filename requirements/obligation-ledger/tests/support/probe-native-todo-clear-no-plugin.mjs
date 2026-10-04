// requirements/obligation-ledger/tests/support/probe-native-todo-clear-no-plugin.mjs
//
// Investigation probe for WHAT[obligation-ledger-002] (ob002 root-cause card):
// the corrected no-plugin control for the empty-array todowrite clear.
//
// Why the seventh-batch control was invalid: it reported
// VERDICT=HOST-CLEARS while its todowrite tool loop never executed
// (set-round=false, after-set=0) — "cleared" was actually "never set".
// That control script was never committed, so its exact defect cannot be
// replayed. This probe rebuilds the control from the proven canary loop
// with the defect class designed out:
//
//   1. provider `test`, model `test-model` and agent `engineer` are all
//      registered by the harness OPENCODE_CONFIG_CONTENT (isolated-env.js),
//      not by the plugin — the loop has no structural dependency on plugins.
//   2. The set round is a HARD GATE: the probe fails closed unless the
//      tool-result round for OL002_A_SET arrives and the A table then holds
//      the submitted rows. The clear round only runs after set is proven.
//   3. Every provider round logs the tool names it saw and the tool-result
//      text it received — direct observation of the no-plugin tool surface.
//   4. The /event SSE stream is recorded for todo.updated events, giving a
//      second oracle beside GET /session/{id}/todo.
//
// Mechanical verdict in the JSON output:
//   HOST-CLEARS-WITHOUT-PLUGIN  — A table empty after clear  (H1 excluded, H2 supported)
//   HOST-NO-OP                  — A table still holds the set rows (H1 supported)
//   HOST-EVENT-BUT-STALE        — empty todo.updated seen, table still holds rows
//   FAIL_*                      — probe did not reach a valid comparison (exit 1)
//
// Execution belongs to DevOps:
//   node requirements/obligation-ledger/tests/support/probe-native-todo-clear-no-plugin.mjs
// The probe itself never asserts plugin behavior; it only observes the Host.

import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim().replace(/^v/, '')

const sessionATodos = [
  { content: '重复内容', status: 'pending', priority: 'low' },
  { content: '多行文本\n第二行', status: 'in_progress', priority: 'medium' },
  { content: 'unicode ☕ 与中文混排', status: 'pending', priority: 'high' },
]

const messageText = (body) => (body?.messages ?? [])
  .flatMap((message) => {
    if (typeof message?.content === 'string') return [message.content]
    if (Array.isArray(message?.content)) return message.content.map((item) => item?.text ?? '').filter(Boolean)
    return []
  })
  .join('\n')

const toolResultText = (body) => (body?.messages ?? [])
  .filter((message) => message?.role === 'tool')
  .flatMap((message) => {
    if (typeof message?.content === 'string') return [message.content]
    if (Array.isArray(message?.content)) return message.content.map((item) => item?.text ?? '').filter(Boolean)
    return []
  })
  .join('\n')

const hasToolResult = (body) => (body?.messages ?? []).some((message) => message?.role === 'tool')

const todoCall = (id, todos) => buildToolCallChunks(id, 'todowrite', JSON.stringify({ todos }), 1)

// Per-round evidence: which tools the Host advertised to the provider, and
// what the tool executor answered. This is what lets the report distinguish
// "todowrite missing from the no-plugin tool surface" from "todowrite ran
// but did not clear".
const providerRounds = []
const provider = await startHttpServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`)
  if ((url.pathname === '/v1/models' || url.pathname === '/models') && request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }
  if (url.pathname === '/v1/chat/completions' && request.method === 'POST') {
    const body = await readRequestBody(request)
    const fullPromptText = messageText(body)
    const round = {
      index: providerRounds.length + 1,
      toolNames: (body?.tools ?? []).map((tool) => tool?.function?.name ?? tool?.name).filter(Boolean),
      sawToolResult: hasToolResult(body),
      toolResultExcerpt: toolResultText(body).slice(0, 300),
      // full text is kept for marker matching; the excerpt is display-only
      promptFull: fullPromptText,
      promptExcerpt: fullPromptText.slice(0, 160),
    }
    providerRounds.push(round)
    if (hasToolResult(body)) {
      sendSSE(response, buildTextChunks(`ol002_probe_round_${round.index}`, 'OL002_PROBE_DONE', 1))
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
    sendJSON(response, 500, { error: `unexpected prompt text: ${text.slice(0, 200)}` })
    return
  }
  sendJSON(response, 500, { error: `unexpected ${request.method} ${url.pathname}` })
})

const request = async (baseUrl, method, pathname, body, expectedStatus) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  if (expectedStatus !== undefined && response.status !== expectedStatus) {
    throw new Error(`${method} ${pathname}: HTTP ${response.status}: ${text}`)
  }
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
  if (response.status !== 200) throw new Error(`GET /session/${sessionId}/todo: HTTP ${response.status}: ${text}`)
  return JSON.parse(text)
}

const waitForToolResultRound = async (marker, host) => {
  const deadline = Date.now() + 30000
  const matches = () => providerRounds.some((round) => {
      const text = round.promptFull
      return round.sawToolResult && text.includes(marker)
        && (marker !== 'OL002_A_SET' || !text.includes('OL002_A_CLEAR'))
  })
  while (Date.now() < deadline) {
    if (matches()) return true
    await delay(200)
  }
  return false
}

// Second oracle: the /event SSE stream. todo.updated payloads are recorded
// whole-ish so the report never depends on a guessed event shape.
const todoUpdatedEvents = []
let eventStreamStatus = 'not-started'
let eventAbort = new AbortController()
const startEventStream = (host) => {
  const listening = (async () => {
    try {
      const response = await fetch(`${host.baseUrl}/event`, {
        headers: { 'x-opencode-directory': encodeURIComponent(host.workDir) },
        signal: eventAbort.signal,
      })
      const contentType = response.headers.get('content-type') ?? ''
      if (!response.ok || !contentType.includes('text/event-stream')) {
        eventStreamStatus = `unavailable: HTTP ${response.status} ${contentType}`
        return
      }
      eventStreamStatus = 'open'
      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        let boundary
        while ((boundary = buffer.indexOf('\n\n')) >= 0) {
          const block = buffer.slice(0, boundary)
          buffer = buffer.slice(boundary + 2)
          const data = block.split('\n')
            .filter((line) => line.startsWith('data:'))
            .map((line) => line.slice(5).trim())
            .join('')
          if (!data || data === '[DONE]') continue
          try {
            const event = JSON.parse(data)
            if (event?.type !== 'todo.updated') continue
            const info = event.info ?? event.properties ?? event.detail ?? {}
            const todos = info.todos ?? event.todos ?? null
            todoUpdatedEvents.push({
              at: new Date().toISOString(),
              sessionID: info.sessionID ?? event.sessionID ?? null,
              todosLength: Array.isArray(todos) ? todos.length : null,
              raw: JSON.stringify(event).slice(0, 400),
            })
          } catch {}
        }
      }
      eventStreamStatus = 'closed'
    } catch (error) {
      if (!eventAbort.signal.aborted) eventStreamStatus = `error: ${error.message}`
    }
  })()
  return listening
}

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-todo-clear-no-plugin-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace, { recursive: true })
await initGitWorkspace(workspace)

const host = new ProcessHost()
let sessionA
const evidence = (observed) => ({
  schemaVersion: 1,
  probe: 'no-plugin empty-array todowrite clear control (corrected)',
  launched: `${OPENCODE_BIN} serve --port 0 --hostname 127.0.0.1`,
  versions: { opencode: opencodeVersion, plugin: 'none (pluginPaths omitted)' },
  publicSdk: ['POST /api/session', 'POST /session/{id}/prompt_async', 'GET /session/{id}/todo', 'GET /event'],
  submitted: { sessionASet: sessionATodos, sessionAClear: [] },
  observed: observed ?? {},
  providerRounds: providerRounds.map(({ promptFull, ...rest }) => rest),
  todoUpdatedEvents,
  eventStreamStatus,
  hostStderrTail: host.stderrLog.slice(-4000),
})
const failClosed = (verdict, observed) => {
  process.stdout.write(`${JSON.stringify({ ...evidence(observed), verdict }, null, 2)}\n`)
  process.exitCode = 1
}

try {
  // No pluginPaths: config.plugin stays an empty array. provider/agent/model
  // registration comes from OPENCODE_CONFIG_CONTENT, which is independent of
  // the plugin — this is the structural fact the seventh batch got wrong.
  await host.start({ scenarioDir, providerUrl: `${provider.url}/v1` })
  startEventStream(host)

  sessionA = sessionIdOf(await request(host.baseUrl, 'POST', '/api/session', {
    agent: 'engineer',
    model: { providerID: 'test', id: 'test-model' },
  }, 200))

  // --- SET round: hard gate. No clear observation unless the table is
  // physically proven to hold the submitted rows first.
  await request(host.baseUrl, 'POST', `/session/${sessionA}/prompt_async`, prompt('OL002_A_SET 为会话 A 提交完整待办列表'), 204)
  if (!(await waitForToolResultRound('OL002_A_SET', host))) {
    failClosed('FAIL_SET_ROUND_TIMEOUT', { note: 'tool-result round for OL002_A_SET never arrived; the loop did not execute' })
    throw new Error('set round missing')
  }
  const aAfterSet = await fetchTodos(host.baseUrl, sessionA)
  if (!Array.isArray(aAfterSet) || aAfterSet.length !== sessionATodos.length) {
    failClosed('FAIL_SET_DID_NOT_LAND', { aAfterSet, note: 'set round executed but the A table did not hold the submitted rows' })
    throw new Error('set did not land')
  }

  // --- CLEAR round: the actual experiment.
  await request(host.baseUrl, 'POST', `/session/${sessionA}/prompt_async`, prompt('OL002_A_CLEAR 清空会话 A 的待办'), 204)
  if (!(await waitForToolResultRound('OL002_A_CLEAR', host))) {
    failClosed('FAIL_CLEAR_ROUND_TIMEOUT', { aAfterSet })
    throw new Error('clear round missing')
  }
  const aAfterClear = await fetchTodos(host.baseUrl, sessionA)
  await delay(1000)
  const aAfterClearSettled = await fetchTodos(host.baseUrl, sessionA)

  const settledRows = Array.isArray(aAfterClearSettled) ? aAfterClearSettled.length : null
  const emptyTodoEvent = todoUpdatedEvents.some((event) => event.todosLength === 0) || null
  let verdict
  if (settledRows === 0) {
    verdict = 'HOST-CLEARS-WITHOUT-PLUGIN'
  } else {
    verdict = settledRows === sessionATodos.length
      ? (emptyTodoEvent ? 'HOST-EVENT-BUT-STALE' : 'HOST-NO-OP')
      : 'HOST-PARTIAL-OR-UNKNOWN'
  }

  process.stdout.write(`${JSON.stringify({
    ...evidence({
      afterASet: { sessionA: aAfterSet },
      afterAClear: { sessionA: aAfterClear },
      afterAClearSettled: { sessionA: aAfterClearSettled },
      settledRows,
      emptyTodoUpdatedEventSeen: emptyTodoEvent,
    }),
    verdict,
  }, null, 2)}\n`)
} finally {
  eventAbort.abort()
  if (sessionA && host.baseUrl) {
    try { await request(host.baseUrl, 'POST', `/session/${sessionA}/abort`, {}, 204) } catch {}
  }
  try { await host.stop() } catch {}
  await stopHttpServer(provider.server)
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
