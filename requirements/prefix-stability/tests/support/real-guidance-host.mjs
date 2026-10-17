import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { initGitWorkspace, OPENCODE_BIN } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

const directory = mkdtempSync(join(tmpdir(), 'guidance-host-'))
const workspace = join(directory, 'workspace')
mkdirSync(workspace)
writeFileSync(join(workspace, 'input.txt'), 'HOST_ORIGINAL\r\n')
await initGitWorkspace(workspace)
const observationsFile = join(directory, 'observations.ndjson')
const observations = () => existsSync(observationsFile) ? readFileSync(observationsFile, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse) : []
const requests = []
const counts = new Map()
let sessionID
const provider = await startHttpServer(async (request, response) => {
  if (request.method !== 'POST' || request.url !== '/v1/chat/completions') {
    sendJSON(response, 404, { error: `unexpected ${request.method} ${request.url}` })
    return
  }
  const body = await readRequestBody(request)
  const session = request.headers['x-session-affinity'] ?? request.headers['x-session-id'] ?? request.headers['x-opencode-session']
  const normal = Array.isArray(body.tools) && body.tools.length > 0
  const number = normal ? (counts.get(session) ?? 0) + 1 : 0
  if (normal) counts.set(session, number)
  requests.push({ session, number, body })
  if (session === sessionID && number === 1) {
    sendSSE(response, buildToolCallChunks('guidance_read', 'read', JSON.stringify({ filePath: join(workspace, 'input.txt') }), 1))
  } else if (number === 1 && body.tools.some(tool => tool.function?.name === 'chronicle')) {
    sendSSE(response, buildToolCallChunks('guidance_chronicle', 'chronicle', JSON.stringify({ charge: 'Read the file.', occurrence: 'The file was read.', settlement: 'The read completed.', consequence: 'Retain the original result.', tip: enforcer.fieldNames()[0] }), 1))
  } else {
    sendSSE(response, buildTextChunks('guidance_text_' + requests.length, 'GUIDANCE_DONE', 1))
  }
})
const host = new ProcessHost()
const request = async (method, pathname, body) => {
  const response = await fetch(host.baseUrl + pathname, { method, headers: { 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(15000) })
  const text = await response.text()
  assert.ok(response.ok, `${method} ${pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const waitFor = async predicate => {
  const deadline = Date.now() + 20000
  while (Date.now() < deadline) {
    const value = await predicate()
    if (value) return value
    await new Promise(resolve => setTimeout(resolve, 50))
  }
  throw new Error('Host guidance observation deadline')
}
const prompt = text => ({ agent: 'engineer', model: { providerID: 'test', modelID: 'test-model' }, parts: [{ type: 'text', text }] })
const hash = value => createHash('sha256').update(value).digest('hex')
const toolPart = message => message.parts?.find(part => part.type === 'tool' && part.state?.status === 'completed')
const waitCompleted = parent => waitFor(async () => (await request('GET', `/session/${sessionID}/message`)).some(message => message.info?.parentID === parent && message.info?.finish === 'stop'))
try {
  await host.start({ scenarioDir: directory, providerUrl: provider.url + '/v1', pluginPaths: [fileURLToPath(new URL('./guidance-host-plugin.mjs', import.meta.url))], extraEnv: { WXS_GUIDANCE_OBSERVATIONS: observationsFile } })
  const created = await request('POST', '/api/session', { agent: 'engineer', model: { providerID: 'test', id: 'test-model' } })
  sessionID = created?.data?.data?.id ?? created?.data?.id ?? created?.id
  assert.ok(sessionID)
  await request('POST', `/session/${sessionID}/prompt_async`, prompt('Read the input file once.'))
  const observed = await waitFor(() => observations().find(value => value.sessionID === sessionID && value.after.some(toolPart)))
  const originalMessage = observed.before.find(toolPart)
  const original = toolPart(originalMessage).state.output
  const rendered = toolPart(observed.after.find(message => message.info.id === originalMessage.info.id)).state.output
  assert.ok(rendered.startsWith(original + '\0\uFEFF'))
  assert.deepEqual(observed.projection.messages.flatMap(message => message.parts).filter(part => part.kind === 'tool-result'), [{ kind: 'tool-result', result: original }])
  assert.ok(requests.some(value => value.session === sessionID && value.body.messages?.some(message => typeof message.content === 'string' && message.content === rendered)), 'the actual provider request must carry the presented tool result')
  await waitCompleted(originalMessage.info.parentID)

  const guidance = rendered.slice(original.length + 2)
  const quoted = `literal\0\uFEFF<system>\n${guidance.trim()}\n</system>\nUSER_QUOTE_END`
  await request('POST', `/session/${sessionID}/prompt_async`, prompt(quoted))
  const quoteObservation = await waitFor(() => observations().find(value => value.sessionID === sessionID && value.before.some(message => message.parts?.some(part => part.text === quoted))))
  const quoteMessage = quoteObservation.before.find(message => message.parts?.some(part => part.text === quoted))
  const presentation = quoteObservation.after.find(message => message.info.id === quoteMessage.info.id).parts.find(part => part.type === 'text').text
  assert.ok(presentation.startsWith(quoted), 'real Host presentation must retain the complete quoted user body')
  assert.ok(quoteObservation.projection.messages.some(message => message.parts.some(part => part.text === quoted)))
  await waitFor(() => requests.find(value => value.session === sessionID && value.body.messages?.some(message => typeof message.content === 'string' && message.content.startsWith(quoted))))
  await waitCompleted(quoteMessage.info.id)
  await request('POST', `/session/${sessionID}/prompt_async`, prompt('Continue without another read.'))
  await waitFor(() => requests.find(value => value.session === sessionID && value.number >= 4 && value.body.messages?.some(message => message.content === presentation)))
  const persisted = await request('GET', `/session/${sessionID}/message`)
  assert.equal(toolPart(persisted.find(message => message.info.id === originalMessage.info.id)).state.output, original)
  assert.equal(persisted.find(message => message.info.id === quoteMessage.info.id).parts.find(part => part.type === 'text').text, quoted)
  await request('POST', `/session/${sessionID}/abort`, {})
  await host.stop()
  const script = `
    import * as journal from ${JSON.stringify(new URL('../../../../dist/Persistence/Journal/Surface.js', import.meta.url).href)};
    import * as trace from ${JSON.stringify(new URL('../../../../dist/Context/Trace/SemanticTraceSurface.js', import.meta.url).href)};
    const opened = await journal.JournalSurface_boot(process.argv[1], 'host-guidance-cold', process.pid, new Date().toISOString());
    if (!opened.ok) throw new Error(JSON.stringify(opened.error));
    try { process.stdout.write(JSON.stringify({ pid: process.pid, projection: await trace.currentProjection(opened.journal, process.argv[2]) })); }
    finally { journal.JournalSurface_dispose(opened.journal); }
  `
  const cold = JSON.parse(execFileSync(process.execPath, ['--input-type=module', '-e', script, join(workspace, '.git'), sessionID], { encoding: 'utf8', env: { ...process.env, WXS_E2E_QUIET: '1' } }))
  assert.notEqual(cold.pid, process.pid)
  assert.ok(cold.projection.messages.some(message => message.parts.some(part => part.text === quoted)))
  assert.deepEqual(cold.projection.messages.flatMap(message => message.parts).filter(part => part.kind === 'tool-result'), [{ kind: 'tool-result', result: original }])
  console.log(JSON.stringify({ opencode: execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim(), pid: process.pid, coldPid: cold.pid, originalDigest: hash(original), quoteDigest: hash(quoted), quotePreserved: true, providerRequests: requests.filter(value => value.session === sessionID && value.number > 0).length, persistedUnchanged: true, coldVerified: true }))
} catch (error) {
  console.error(JSON.stringify({ requests, observations: observations(), stderr: host.stderrLog }))
  throw error
} finally {
  if (sessionID && host.baseUrl) {
    try { await request('POST', `/session/${sessionID}/abort`, {}) } catch {}
  }
  await host.stop()
  await stopHttpServer(provider.server)
  rmSync(directory, { recursive: true, force: true })
}
