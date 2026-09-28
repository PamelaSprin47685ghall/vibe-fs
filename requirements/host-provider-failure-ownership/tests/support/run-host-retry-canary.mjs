import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { sendJSON } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const versions = {
  opencode: execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim(),
  plugin: JSON.parse(readFileSync(new URL('../../../../node_modules/@opencode-ai/plugin/package.json', import.meta.url), 'utf8')).version,
}
assert.deepEqual(versions, { opencode: '1.18.29', plugin: '1.18.29' })
const observations = []
const waiters = []
const publish = record => {
  observations.push(record)
  for (let index = waiters.length - 1; index >= 0; index--) {
    if (waiters[index].predicate(record)) waiters.splice(index, 1)[0].resolve(record)
  }
}
const waitFor = predicate => {
  const existing = observations.find(predicate)
  return existing ? Promise.resolve(existing) : new Promise(resolve => { waiters.push({ predicate, resolve }) })
}
const collector = await startHttpServer(async (request, response) => {
  publish(await readRequestBody(request))
  response.writeHead(204).end()
})
const deliveries = []
const provider = await startHttpServer(async (request, response) => {
  const pathname = new URL(request.url, `http://${request.headers.host}`).pathname
  if (request.method === 'GET' && ['/models', '/v1/models'].includes(pathname)) {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
  } else if (request.method === 'POST' && pathname === '/v1/chat/completions') {
    deliveries.push(await readRequestBody(request))
    publish({ kind: 'provider-request', value: { count: deliveries.length } })
    sendJSON(response, 503, { error: { message: 'controlled provider unavailable', type: 'server_error' } }, { 'retry-after-ms': '1' })
  } else {
    sendJSON(response, 404, { error: 'unexpected canary request' })
  }
})
const directory = mkdtempSync(join(tmpdir(), 'wxs-host-retry-canary-'))
const host = new ProcessHost()
let session
const request = async (method, path, body, expected) => {
  const response = await fetch(host.baseUrl + path, {
    method, headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  assert.equal(response.status, expected, `${method} ${path}: ${text}`)
  return text ? JSON.parse(text) : null
}
try {
  mkdirSync(join(directory, 'workspace'))
  await initGitWorkspace(join(directory, 'workspace'))
  await host.start({
    scenarioDir: directory,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [fileURLToPath(new URL('./retry-canary-plugin.mjs', import.meta.url))],
    extraEnv: { WANXIANGSHU_RETRY_CANARY_COLLECTOR: collector.url, WANXIANGSHU_CHAT_MAX_RETRIES: '8', WANXIANGSHU_PROVIDER_LANGUAGE: 'en' },
  })
  const configured = await waitFor(record => record.kind === 'config')
  assert.equal(configured.value.retries, 0)
  const created = await request('POST', '/session', {
    agent: 'engineer', title: 'Retry ownership canary', model: { providerID: 'test', id: 'test-model' },
  }, 200)
  session = created?.data?.data?.id ?? created?.data?.id ?? created?.id
  assert.ok(session)
  assert.equal((created?.data?.data ?? created?.data ?? created).title, 'Retry ownership canary', 'explicit title prevents a background title-model request')
  const physical = 'msg_retry_canary_failure'
  await request('POST', `/session/${session}/prompt_async`, {
    messageID: physical, agent: 'engineer', model: { providerID: 'test', modelID: 'test-model' },
    parts: [{ type: 'text', text: 'controlled retry ownership probe' }],
  }, 204)
  await Promise.race([
    Promise.all([
      waitFor(record => record.kind === 'session.error' && record.value.session === session),
      waitFor(record => record.kind === 'assistant' && record.value.parent === physical && record.value.completed && record.value.errored),
      waitFor(record => record.kind === 'session.idle' && record.value.session === session),
    ]),
    waitFor(record => record.kind === 'provider-request' && record.value.count > 1),
  ])
  const runs = new Set(observations.filter(record => record.kind === 'assistant' && record.value.parent === physical).map(record => record.value.id))
  process.stdout.write(JSON.stringify({ versions, configuredRetries: configured.value.retries, providerRequests: deliveries.length, providerRuns: runs.size,
    hostErrorObserved: observations.some(record => record.kind === 'session.error'),
    terminalObserved: observations.some(record => record.kind === 'assistant' && record.value.completed && record.value.errored),
  }) + '\n')
  assert.equal(deliveries.length, 1, 'one failed physical run must reach the provider once')
  assert.equal(runs.size, 1)
} finally {
  if (session && host.baseUrl) {
    try { await request('POST', `/session/${session}/abort`, {}, 204) } catch {}
  }
  try {
    await host.stop()
  } finally {
    await stopHttpServer(provider.server)
    await stopHttpServer(collector.server)
    rmSync(directory, { recursive: true, force: true })
  }
}
