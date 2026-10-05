import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { EventProbe } from '../../../verification-system/tests/e2e/support/event-probe.js'
import { initGitWorkspace, OPENCODE_BIN } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, buildToolCallsChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../../..')
const version = '1.18.29'
const marker = 'NATIVE_READ_PROTOCOL_CANARY'
const shortLines = ['第一行 😀', 'second line']
const numberedRows = length => Array.from({ length }, (_, index) => `row ${index + 1}`)
const byteRows = length => Array.from({ length }, (_, index) => `${index + 1}:${'字'.repeat(500)}`)
const cases = [
  { name: 'lf', raw: '第一行 😀\nsecond line\n', lines: shortLines, start: 1, total: 2, truncated: false, suffix: '(End of file - total 2 lines)' },
  { name: 'crlf', raw: '第一行 😀\r\nsecond line\r\n', lines: shortLines, start: 1, total: 2, truncated: false, suffix: '(End of file - total 2 lines)' },
  { name: 'no-final-lf', raw: '第一行 😀\nsecond line', lines: shortLines, start: 1, total: 2, truncated: false, suffix: '(End of file - total 2 lines)' },
  { name: 'partial', raw: 'first\n第二行 😀\nthird\n', range: { offset: 2, limit: 1 }, lines: ['第二行 😀'], start: 2, total: 3, truncated: true, suffix: '(Showing lines 2-2 of 3. Use offset=3 to continue.)' },
  { name: 'line-limit', raw: numberedRows(2002).join('\n'), lines: numberedRows(2000), start: 1, total: 2002, truncated: true, suffix: '(Showing lines 1-2000 of 2002. Use offset=2001 to continue.)' },
  { name: 'long-line', raw: `before\n${'x'.repeat(2100)}😀\nafter\n`, lines: ['before', `${'x'.repeat(2000)}... (line truncated to 2000 chars)`, 'after'], start: 1, total: 3, truncated: false, suffix: '(End of file - total 3 lines)' },
  { name: 'byte-limit', raw: byteRows(70).join('\n'), lines: byteRows(34), start: 1, total: 35, truncated: true, suffix: '(Output capped at 50 KB. Showing lines 1-34. Use offset=35 to continue.)' },
]
const expectedOutput = sample => `<path>${sample.args.filePath}</path>\n<type>file</type>\n<content>\n${sample.lines.map((line, index) => `${sample.start + index}: ${line}`).join('\n')}\n\n${sample.suffix}\n</content>`
const expectedMetadata = sample => ({
  preview: sample.lines.slice(0, 20).join('\n'), truncated: sample.truncated, loaded: [],
  display: {
    type: 'file', path: sample.args.filePath, text: sample.lines.join('\n'),
    lineStart: sample.start, lineEnd: sample.start + sample.lines.length - 1,
    totalLines: sample.total, truncated: sample.truncated,
  },
})
const host = new ProcessHost()
const observations = []
const requests = []
const callbacks = new Set()
const providerErrors = []
const failures = []
const cleanupErrors = []
const evidenceDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiang-native-read-evidence-'))
let provider
let scenarioDir
let probe
let timer
let sessionID
let transcript
let result
let resolveObserved
let rejectObserved
const observed = new Promise((resolve, reject) => {
  resolveObserved = resolve
  rejectObserved = reject
})
observed.catch(() => {})
const api = async (pathname, body) => {
  const response = await fetch(host.baseUrl + pathname, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'content-type': 'application/json', 'x-opencode-directory': encodeURIComponent(host.workDir) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  assert.ok(response.ok, `${pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const valueOf = response => response?.data?.data ?? response?.data ?? response
const callID = index => `native-read-protocol_${index + 1}`
const resultsOf = body => (body.messages ?? []).filter(message => message.role === 'tool' && cases.some((_, index) => message.tool_call_id === callID(index)))
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
    if (request.url === '/observer') {
      observations.push(body)
      sendJSON(response, 200, { accepted: true })
      return
    }
    const hasRead = (body.tools ?? []).some(tool => (tool.function?.name ?? tool.name) === 'read')
    const hasMarker = (body.messages ?? []).some(message => typeof message.content === 'string' && message.content.includes(marker))
    if (!hasRead || !hasMarker) {
      sendSSE(response, buildTextChunks('native-read-title', 'Native read protocol canary', 1))
      return
    }
    requests.push({ bytesBase64: bytes.toString('base64'), body })
    if (requests.length === 1) {
      assert.equal(resultsOf(body).length, 0)
      sendSSE(response, buildToolCallsChunks('native-read-protocol', cases.map(sample => ({
        name: 'read', argsStr: JSON.stringify(sample.args),
      })), 1))
      return
    }
    assert.equal(requests.length, 2, 'one completed native read batch must cause exactly one next inference')
    assert.equal(resultsOf(body).length, cases.length, 'the actual next provider request must contain every read result')
    sendSSE(response, buildTextChunks('native-read-done', 'Native read protocol captured.', 1))
    resolveObserved()
  } catch (error) {
    providerErrors.push(error)
    rejectObserved(error)
    sendJSON(response, 500, { error: error.message })
  }
}

try {
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'node_modules/opencode-ai/package.json'), 'utf8')).version, version)
  assert.equal(fs.realpathSync(OPENCODE_BIN), fs.realpathSync(path.join(root, 'node_modules/.bin/opencode')), 'the canary must use the installed repository Host')
  provider = await startHttpServer((request, response) => {
    const callback = handleProviderRequest(request, response).catch(error => {
      providerErrors.push(error)
      rejectObserved(error)
    })
    callbacks.add(callback)
    callback.finally(() => callbacks.delete(callback))
  })
  scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiang-native-read-'))
  const workspace = path.join(scenarioDir, 'workspace')
  fs.mkdirSync(workspace)
  for (const sample of cases) {
    sample.args = { filePath: path.join(workspace, `${sample.name}.txt`), ...sample.range }
    fs.writeFileSync(sample.args.filePath, sample.raw, 'utf8')
  }
  fs.writeFileSync(path.join(evidenceDir, 'oracle.json'), JSON.stringify(cases.map(sample => ({
    name: sample.name, args: sample.args, rawBytesBase64: Buffer.from(sample.raw).toString('base64'),
    expectedOutput: expectedOutput(sample), expectedMetadata: expectedMetadata(sample),
  })), null, 2))
  await initGitWorkspace(workspace)
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [path.join(here, 'native-read-canary-observer.mjs')],
    extraEnv: { WXS_NATIVE_READ_CANARY_COLLECTOR: `${provider.url}/observer` },
  })
  assert.equal((await api('/global/health')).version, version, 'the live Host must expose the pinned build version')
  probe = new EventProbe(host.baseUrl, host.workDir)
  await probe.connect()
  sessionID = valueOf(await api('/session', { title: 'Native read protocol canary' })).id
  timer = setTimeout(() => rejectObserved(new Error(`native read observations missing: ${observations.length}/${cases.length}; provider requests ${requests.length}`)), 30000)
  await api(`/session/${sessionID}/prompt_async`, {
    messageID: 'msg_native_read_protocol_canary', agent: 'engineer',
    model: { providerID: 'test', modelID: 'test-model' },
    parts: [{ type: 'text', text: marker }],
  })
  await observed
  await probe.awaitEvent(event => event.type === 'session.idle' && event.properties?.sessionID === sessionID, 10000)
  transcript = valueOf(await api(`/session/${sessionID}/message`))
  assert.ok(Array.isArray(transcript), 'the SDK message snapshot must be a concrete array')
  const tools = transcript.flatMap(message => message.parts ?? []).filter(part => part.type === 'tool')
  assert.equal(observations.length, cases.length, 'every read must have exactly one public after-hook observation')
  for (const [index, sample] of cases.entries()) {
    const seen = observations.filter(observation => observation.input.callID === callID(index))
    assert.equal(seen.length, 1, `${sample.name}: after hook identity must be exact and unique`)
    const observation = seen[0]
    assert.equal(observation.input.tool, 'read')
    assert.equal(observation.input.sessionID, sessionID)
    assert.deepEqual(observation.input.args, sample.args)
    assert.equal(observation.output.output, expectedOutput(sample), `${sample.name}: the whole public wrapper must match the pinned byte oracle`)
    assert.equal(observation.output.title, path.relative(host.workDir, sample.args.filePath))
    assert.deepEqual(observation.output.metadata, expectedMetadata(sample), `${sample.name}: every public metadata field must match the pinned oracle`)
    const completed = tools.filter(part => part.callID === callID(index))
    assert.equal(completed.length, 1, `${sample.name}: SDK tool identity must be exact and unique`)
    assert.equal(completed[0].state.status, 'completed')
    assert.deepEqual(completed[0].state.input, sample.args)
    assert.equal(completed[0].state.output, observation.output.output)
    assert.equal(completed[0].state.title, observation.output.title)
    assert.deepEqual(completed[0].state.metadata, observation.output.metadata)
    const wire = resultsOf(requests[1].body).filter(message => message.tool_call_id === callID(index))
    assert.equal(wire.length, 1, `${sample.name}: provider tool result identity must be exact and unique`)
    assert.equal(wire[0].content, observation.output.output, `${sample.name}: the provider must receive the exact public tool output string`)
  }
  const outputOf = name => observations.find(observation => observation.input.args.filePath === cases.find(sample => sample.name === name).args.filePath).output
  const canonicalShort = outputOf('no-final-lf')
  for (const name of ['lf', 'crlf']) {
    const sample = cases.find(sample => sample.name === name)
    const observedOutput = outputOf(name)
    assert.notEqual(sample.raw, cases.find(sample => sample.name === 'no-final-lf').raw)
    assert.equal(observedOutput.output.replace(sample.args.filePath, '<fixture>'), canonicalShort.output.replace(cases[2].args.filePath, '<fixture>'), `${name}: distinct raw bytes must collide in the public output`)
    assert.deepEqual({ ...observedOutput.metadata, display: { ...observedOutput.metadata.display, path: '<fixture>' } },
      { ...canonicalShort.metadata, display: { ...canonicalShort.metadata.display, path: '<fixture>' } }, `${name}: metadata must also discard the original line ending and final newline`)
  }
  assert.equal(outputOf('long-line').metadata.truncated, false)
  assert.notEqual(outputOf('long-line').metadata.display.text, cases.find(sample => sample.name === 'long-line').raw)
  assert.equal(outputOf('long-line').output.includes('😀'), false, 'an EOF result with truncated=false still discards actual long-line bytes')
  assert.equal(outputOf('byte-limit').metadata.display.totalLines, 35, 'a byte-capped totalLines value does not describe the complete 70-line file')
  if (process.argv.includes('--late-provider-error')) {
    const response = await fetch(`${provider.url}/v1/chat/completions`, { method: 'POST', body: '{' })
    assert.equal(response.status, 500, 'the late request must actually enter the provider error path')
    await response.text()
  }
  result = {
    version, cases: cases.length, exactOutputAndMetadataOracle: true,
    afterHookMatchesSDK: true, nextProviderMatchesAfterHook: true,
    lineEndingCollision: true, longLineTruncationUnmarked: true,
    rawFileBytesRecoverable: false, nativeByteCoverage: 'PartialFile', evidenceDir,
  }
} catch (error) {
  failures.push(error)
} finally {
  clearTimeout(timer)
  for (const cleanup of [() => probe?.close(), () => host.stop(), () => provider && stopHttpServer(provider.server),
    () => Promise.all(callbacks)]) {
    try { await cleanup() } catch (error) { cleanupErrors.push(error) }
  }
  fs.writeFileSync(path.join(evidenceDir, 'observations.json'), JSON.stringify(observations, null, 2))
  fs.writeFileSync(path.join(evidenceDir, 'provider-requests.json'), JSON.stringify(requests, null, 2))
  fs.writeFileSync(path.join(evidenceDir, 'sdk-transcript.json'), JSON.stringify(transcript ?? null, null, 2))
  fs.writeFileSync(path.join(evidenceDir, 'events.json'), JSON.stringify(probe?.allEvents ?? [], null, 2))
  fs.writeFileSync(path.join(evidenceDir, 'host.stdout.log'), host.stdoutLog)
  fs.writeFileSync(path.join(evidenceDir, 'host.stderr.log'), host.stderrLog)
  try { if (scenarioDir) fs.rmSync(scenarioDir, { recursive: true, force: true }) } catch (error) { cleanupErrors.push(error) }
}
const errors = [...new Set([...failures, ...providerErrors, ...cleanupErrors])]
if (errors.length) console.error(`native-read-canary-failure: ${JSON.stringify({
  evidenceDir, requests: requests.length,
  failures: failures.map(error => ({ name: error.name, message: error.message })),
  providerErrors: providerErrors.map(error => ({ name: error.name, message: error.message })),
  cleanupErrors: cleanupErrors.map(error => ({ name: error.name, message: error.message })),
})}`)
if (errors.length === 1) throw errors[0]
if (errors.length > 1) throw new AggregateError(errors, 'Native read canary failed', { cause: errors[0] })
assert.equal(requests.length, 2, 'shutdown must not conceal extra provider inference requests')
console.log(`native-read-canary: ${JSON.stringify(result)}`)
