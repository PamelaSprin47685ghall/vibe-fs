/**
 * run-readonly-delegation-schema-canary.mjs — real-Host canary for the readonly
 * delegation schema contract (speculative-investigation-013):
 *
 *  - the production plugin is loaded by a real OpenCode host against a mock
 *    provider, with a Predictor model configured in the isolated
 *    `wanxiangshu.mjs` (the only enablement condition);
 *  - the provider wire is inspected: participating tools must carry the
 *    required `estimated_readonly_rounds` integer budget and the optional
 *    `self_note`, including Host built-ins whose definition carries an Effect
 *    argument schema and no JSON schema (`read`, `glob`, `grep`, …);
 *  - original tool constraints survive decoration (built-in `required` entries
 *    are preserved);
 *  - a built-in tool call that carries the protocol fields still executes: the
 *    arguments are hidden before the Host decodes them with the tool's own
 *    Effect schema, and the provider-wire history keeps the original call.
 *
 * Prints one JSON summary on stdout; the wrapping test asserts on it.
 */

import assert from 'node:assert/strict'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { toolSpecNames } from '../../../../dist/OpenCode/Tools/ToolSurface.js'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import {
  buildTextChunks,
  buildToolCallChunks,
  sendJSON,
  sendSSE,
} from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '../../../..')
const productionPluginPath = path.join(repoRoot, 'dist', 'OpenCode', 'Plugin', 'Plugin.js')

const CONFIGURED_ROUTING = `export const routingProtocol = 2
export const hasTheoreticalCapacity = (role, purpose) => true
export const predictorConfiguration = () => ({ state: 'configured', reason: null })
export default function route(role, running, previous, purpose) {
  return { model: 'test/test-model', reasoning: 'none' }
}
`

const READ_FILE = 'canary-sample.txt'
const FILE_BODY = 'readonly delegation schema canary\n'
const CALL_ID = 'call_read_delegated_0'
const BUDGET = 2
const SELF_NOTE = 'checking the canary fixture'

const EXPECTED_PARTICIPATING_NAMES = [
  'read', 'glob', 'grep', 'js-manager', 'js-engineer', 'js-devops',
  'edit', 'write', 'mv', 'rm', 'fetch', 'run',
]

const known = new Set(toolSpecNames())
const PARTICIPATING_TOOLS = new Set(EXPECTED_PARTICIPATING_NAMES)

assert.equal(
  PARTICIPATING_TOOLS.size,
  12,
  `expected exactly 12 participating tools, got ${PARTICIPATING_TOOLS.size}`,
)
assert.ok(
  EXPECTED_PARTICIPATING_NAMES.every((name) => known.has(name)),
  'all participating tools must be known canonical tools from ToolSurface',
)

const isTitleRequest = (body) => {
  const messages = Array.isArray(body?.messages) ? body.messages : []
  return messages.some(
    (message) =>
      typeof message?.content === 'string' &&
      (message.content.startsWith('Generate a title for this conversation:') ||
        message.content.includes('title generator')),
  )
}

const providerRequests = []
const wireTools = new Map()
let sessionID = null

// The engineer session is identified on the wire by carrying the built-in
// `read` tool; unrelated sessions (companion, chronicle) reuse the provider and
// may arrive first, so neither arrival order nor request counting identifies it.
const isEngineerRequest = (body) =>
  (body?.tools ?? []).some((tool) => (tool?.function?.name ?? tool?.name) === 'read')

const provider = http.createServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`)

  if ((url.pathname === '/v1/models' || url.pathname === '/models') && request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }

  if (url.pathname === '/v1/chat/completions' && request.method === 'POST') {
    const chunks = []
    for await (const chunk of request) chunks.push(chunk)
    const body = JSON.parse(Buffer.concat(chunks).toString('utf8'))
    providerRequests.push(body)

    if (isTitleRequest(body)) {
      sendSSE(response, buildTextChunks('title_1', 'Readonly delegation schema canary', 1))
      return
    }

    if (wireTools.size === 0 && isEngineerRequest(body)) {
      for (const tool of body.tools ?? []) {
        const name = tool?.function?.name ?? tool?.name
        if (typeof name !== 'string') continue
        const parameters = tool?.function?.parameters ?? {}
        wireTools.set(name, {
          description: tool?.function?.description ?? '',
          required: Array.isArray(parameters.required) ? [...parameters.required] : [],
          properties: Object.keys(parameters.properties ?? {}),
          budget: parameters.properties?.estimated_readonly_rounds ?? null,
          note: parameters.properties?.self_note ?? null,
          isParticipating: PARTICIPATING_TOOLS.has(name),
        })
      }

      const args = JSON.stringify(
        BUDGET > 0
          ? { filePath: READ_FILE, estimated_readonly_rounds: BUDGET, self_note: SELF_NOTE }
          : { filePath: READ_FILE, estimated_readonly_rounds: BUDGET },
      )
      sendSSE(response, buildToolCallChunks(CALL_ID, 'read', args, 10))
      return
    }

    sendSSE(response, buildTextChunks('canary_done', 'READONLY_DELEGATION_SCHEMA_CANARY_DONE', 20))
    return
  }

  sendJSON(response, 404, { error: `unexpected ${request.method} ${url.pathname}` })
})

await new Promise((resolve) => provider.listen(0, '127.0.0.1', resolve))

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-delegation-schema-canary-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace, { recursive: true })
fs.writeFileSync(path.join(workspace, READ_FILE), FILE_BODY, 'utf8')
await initGitWorkspace(workspace)

const host = new ProcessHost()

const request = async (baseUrl, method, pathname, body, expectedStatus) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()

  if (expectedStatus !== undefined && response.status !== expectedStatus) {
    throw new Error(`${method} ${pathname}: expected ${expectedStatus}, got ${response.status}: ${text}`)
  }
  return text ? JSON.parse(text) : null
}

const sessionIdOf = (payload) =>
  payload?.data?.data?.data?.id ?? payload?.data?.data?.id ?? payload?.data?.id ?? payload?.id

const summary = () => {
  const names = [...wireTools.keys()].sort()

  const participating = {}
  const nonParticipating = {}
  for (const [name, view] of wireTools.entries()) {
    if (PARTICIPATING_TOOLS.has(name)) {
      participating[name] = view
    } else {
      nonParticipating[name] = view
    }
  }

  // The follow-up request to our own tool call is the one whose history carries
  // the result for CALL_ID; the engineer session is not the only session the
  // host talks to (companion sessions reuse the same provider).
  const followUp = providerRequests.find((body) =>
    (body?.messages ?? []).some((message) => message?.tool_call_id === CALL_ID),
  )

  const historicalCall = (followUp?.messages ?? [])
    .filter((message) => message?.role === 'assistant')
    .flatMap((message) => message.tool_calls ?? [])
    .find((call) => call?.id === CALL_ID)

  let historicalArguments = null
  try {
    historicalArguments = historicalCall ? JSON.parse(historicalCall.function.arguments) : null
  } catch {
    historicalArguments = null
  }

  const toolResult = (followUp?.messages ?? []).find((message) => message?.tool_call_id === CALL_ID) ?? null

  return {
    schemaVersion: 1,
    providerRequests: providerRequests.length,
    visibleTools: names,
    tools: Object.fromEntries(wireTools),
    participatingTools: participating,
    nonParticipatingTools: nonParticipating,
    read: wireTools.get('read') ?? null,
    followUpObserved: followUp !== undefined,
    historicalArguments,
    toolResultPreview: typeof toolResult?.content === 'string' ? toolResult.content.slice(0, 400) : null,
  }
}

try {
  await host.start({
    scenarioDir,
    providerUrl: `http://127.0.0.1:${provider.address().port}/v1`,
    pluginPaths: [productionPluginPath],
    routingSource: CONFIGURED_ROUTING,
  })

  const created = await request(
    host.baseUrl,
    'POST',
    '/api/session',
    { agent: 'engineer', model: { providerID: 'test', id: 'test-model' } },
    200,
  )
  sessionID = sessionIdOf(created)

  if (!sessionID) throw new Error(`session creation returned no id: ${JSON.stringify(created)}`)

  await request(
    host.baseUrl,
    'POST',
    `/session/${sessionID}/prompt_async`,
    {
      messageID: 'msg_canary_readonly_delegation_1',
      agent: 'engineer',
      model: { providerID: 'test', modelID: 'test-model' },
      parts: [{ type: 'text', text: 'READ_THE_CANARY_FIXTURE' }],
    },
    204,
  )

  // The engineer session's follow-up is the request whose history carries the
  // result of our tool call; unrelated sessions (companion, chronicle) share
  // the same mock provider and may reach it first.
  const followUpObserved = () =>
    providerRequests.some((body) =>
      (body?.messages ?? []).some((message) => message?.tool_call_id === CALL_ID),
    )

  const deadline = Date.now() + 45000
  while (!followUpObserved() && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 200))
  }

  // One machine-readable line; the wrapping test parses after this marker.
  process.stdout.write(`READONLY_DELEGATION_SCHEMA_CANARY ${JSON.stringify(summary())}\n`)
} catch (error) {
  console.error('[run-readonly-delegation-schema-canary] canary failed:', error)
  console.error('--- HOST STDOUT ---')
  console.error(host.stdoutLog.slice(-4000))
  console.error('--- HOST STDERR ---')
  console.error(host.stderrLog.slice(-4000))
  process.exitCode = 1
} finally {
  if (sessionID && host.baseUrl) {
    try {
      await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, 204)
    } catch {}
  }
  try {
    await host.stop()
  } catch {}
  await new Promise((resolve) => provider.close(resolve))
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
