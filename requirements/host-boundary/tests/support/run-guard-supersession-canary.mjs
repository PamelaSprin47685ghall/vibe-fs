import assert from 'node:assert/strict'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { execFileSync, spawnSync } from 'node:child_process'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
import { stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js'
import { queryFacts } from '../../../../dist/Execution/Session/ChatExecution/StatusSurface.js'

const root = path.resolve(import.meta.dirname, '../../../..')
const capacityOne = process.argv.includes('--capacity-one')
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim()
assert.equal(opencodeVersion, '1.18.29')
const phases = capacityOne ? ['STARTED', 'PAUSED', 'INTERLEAVED'] : ['PAUSED', 'STARTED', 'INTERLEAVED']
const phaseArgument = process.argv.indexOf('--phase')
const selectedPhase = phaseArgument < 0 ? undefined : process.argv[phaseArgument + 1]
if (selectedPhase === undefined) {
  const results = []
  for (const phase of phases) {
    const arguments_ = [import.meta.filename, '--phase', phase]
    if (capacityOne) arguments_.push('--capacity-one')
    const launched = spawnSync(process.execPath, arguments_, { encoding: 'utf8', timeout: 120000 })
    assert.equal(launched.status, 0, `${launched.error ?? ''}\n${launched.stdout}\n${launched.stderr}`)
    const result = JSON.parse(launched.stdout.trim())
    assert.equal(result.opencode, opencodeVersion)
    assert.equal(result.capacityOne, capacityOne)
    assert.equal(result.physicalCleanup, true)
    assert.equal(result.results.length, 1)
    assert.equal(result.results[0].phase, phase)
    results.push(result.results[0])
  }
  process.stdout.write(`${JSON.stringify({ opencode: opencodeVersion, capacityOne, results, physicalCleanup: true })}\n`)
  process.exit(0)
}
assert.ok(phases.includes(selectedPhase), 'the selected physical phase must be declared')
const observations = []
const waiters = new Set()
const releases = new Map()
const released = new Set()
const publish = (kind, value) => {
  if (kind === 'fixture.failure') process.exitCode = 1
  const observation = { sequence: observations.length + 1, kind, value }
  observations.push(observation)
  for (const waiter of waiters) {
    if (kind === 'fixture.failure') waiter.reject(new Error(value.error))
    else if (waiter.matches(observation)) waiter.resolve(observation)
  }
}
const waitFor = (matches) => {
  const failure = observations.find(({ kind }) => kind === 'fixture.failure')
  if (failure) return Promise.reject(new Error(failure.value.error))
  const existing = observations.find(matches)
  if (existing) return Promise.resolve(existing)
  return new Promise((resolve, reject) => {
    const waiter = {
      matches,
      resolve: value => {
        clearTimeout(timer)
        waiters.delete(waiter)
        resolve(value)
      },
      reject: error => {
        clearTimeout(timer)
        waiters.delete(waiter)
        reject(error)
      },
    }
    const timer = setTimeout(() => {
      waiters.delete(waiter)
      reject(new Error(`Missing observation: ${matches}`))
    }, 25000)
    waiters.add(waiter)
  })
}
const listen = async (server) => {
  await new Promise((resolve, reject) => {
    server.once('error', reject)
    server.listen(0, '127.0.0.1', resolve)
  })
  return `http://127.0.0.1:${server.address().port}`
}
const bodyOf = async (request) => {
  const chunks = []
  for await (const chunk of request) chunks.push(chunk)
  return JSON.parse(Buffer.concat(chunks).toString('utf8'))
}
const collector = http.createServer(async (request, response) => {
  if (request.method === 'GET') {
    const key = decodeURIComponent(request.url.slice('/release/'.length))
    if (released.has(key)) response.writeHead(204).end()
    else releases.set(key, response)
    return
  }
  const observation = await bodyOf(request)
  publish(observation.kind, observation.value)
  response.writeHead(204).end()
})
const collectorUrl = await listen(collector)
const release = (key) => {
  released.add(key)
  releases.get(key)?.writeHead(204).end()
  releases.delete(key)
}
const handleProvider = async (request, response) => {
  if (request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }
  const body = await bodyOf(request)
  const names = (body.tools ?? []).map(tool => tool.function?.name ?? tool.name)
  if (!names.includes('js-manager')) {
    const chronicle = body.tools?.find(tool => tool.function?.name === 'chronicle')
    if (chronicle) {
      const tip = chronicle.function.parameters.properties.tip.enum[0]
      sendSSE(response, buildToolCallChunks('chronicle', 'chronicle', JSON.stringify({
        charge: 'Preserve the controlled supersession evidence.', occurrence: 'A fresh user input arrived.',
        settlement: 'The exact execution owns its observation.', consequence: 'No unrelated execution is cancelled.', tip,
      }), 1))
    } else sendSSE(response, buildTextChunks('auxiliary', 'Auxiliary response.', 1))
    return
  }
  const latest = body.messages?.findLast(message => message.role === 'user')
  const text = typeof latest?.content === 'string' ? latest.content : JSON.stringify(latest?.content)
  if (text.includes('SUPERSESSION_HUMAN')) {
    publish('human.provider', { text, sessionID: request.headers['x-wxs-canary-session'], messageID: request.headers['x-wxs-canary-message'] })
    sendSSE(response, buildTextChunks('human', 'HUMAN_ANSWER', 1))
  } else if (text.includes('SUPERSESSION_PAUSED_ROOT') || text.includes('SUPERSESSION_STARTED_ROOT') || text.includes('SUPERSESSION_INTERLEAVED_ROOT')) {
    sendSSE(response, buildTextChunks('root', 'ROOT_ANSWER', 1))
  } else {
    const sessionID = request.headers['x-wxs-canary-session']
    const messageID = request.headers['x-wxs-canary-message']
    assert.equal(typeof sessionID, 'string', 'provider observation requires the public chat.headers session')
    assert.equal(typeof messageID, 'string', 'provider observation requires the public chat.headers physical user message')
    assert.ok(observations.some(({ kind, value }) => kind === 'message.accepted' && value.sessionID === sessionID && value.messageID === messageID && value.origin === 'ManagerGuard'), 'unknown manager request cannot stand in for a Guard')
    const requestID = observations.length + 1
    publish('guard.provider', { text, requestID, sessionID, messageID })
    response.once('close', () => publish('guard.provider.closed', { text, requestID, sessionID, messageID }))
  }
}
const provider = http.createServer((request, response) => {
  handleProvider(request, response).catch(error => {
    publish('fixture.failure', { error: String(error) })
    sendJSON(response, 500, { error: String(error) })
  })
})
const providerUrl = await listen(provider)
const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-guard-supersession-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace)
await initGitWorkspace(workspace)
const host = new ProcessHost()
const sessions = []
const request = async (method, pathname, body) => {
  const response = await fetch(host.baseUrl + pathname, {
    method, headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(25000),
  })
  const text = await response.text()
  assert.equal(response.ok, true, `${method} ${pathname}: HTTP ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const prompt = (messageID, text) => ({
  messageID, agent: 'manager', model: { providerID: 'test', modelID: 'test-model' }, parts: [{ type: 'text', text }],
})
const facts = () => {
  const directory = path.join(workspace, '.git/wanxiang/events')
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.ndjson'))
  assert.equal(files.length, 1, 'this isolated Host proof requires a single durable writer')
  return files.flatMap(name =>
    fs.readFileSync(path.join(directory, name), 'utf8').trim().split('\n').filter(Boolean).map(line => JSON.parse(line).payload?.Fact),
  ).filter(fact => fact?.[0] === 'Agent' && fact[1]?.[0] === 'ChatExecution').map(fact => JSON.stringify(fact))
}
const terminalCount = (serializedFacts, sessionID, messageID) => serializedFacts.map(value => JSON.parse(value)[1][1]).filter(([kind, payload]) =>
  kind === 'Terminal' && payload.Key.SessionId[1] === sessionID && payload.Key.PhysicalUserMessageId[1] === messageID).length
const results = []
try {
  await host.start({
    scenarioDir, providerUrl: `${providerUrl}/v1`,
    pluginPaths: [path.join(import.meta.dirname, 'guard-supersession-canary-plugin.mjs')],
    extraEnv: { WXS_SUPERSESSION_PLUGIN: path.join(root, 'dist/OpenCode/Plugin/Plugin.js'), WXS_SUPERSESSION_COLLECTOR: collectorUrl },
    ...(capacityOne ? { routingSource: `export const routingProtocol = 2
export const hasTheoreticalCapacity = () => true
export const predictorConfiguration = () => ({ state: 'unconfigured', reason: null })
export default function route(role, running) {
  if (role !== 'manager') return { model: 'test/test-model-b', reasoning: 'none' }
  return running.filter(target => target.model === 'test/test-model').length < 1
    ? { model: 'test/test-model', reasoning: 'none' }
    : null
}
` } : {}),
  })
  for (const phase of [selectedPhase]) {
    const phaseStart = observations.length
    const session = await request('POST', '/session', { title: `Guard supersession ${phase}` })
    const sessionID = session.id
    assert.equal(typeof sessionID, 'string')
    sessions.push(sessionID)
    const first = await request('POST', `/session/${sessionID}/message`, prompt(`msg_root_${phase}`, `SUPERSESSION_${phase}_ROOT`))
    assert.equal(first.info.parentID, `msg_root_${phase}`)
    const guard = await waitFor(({ kind, value }) => kind === 'message.accepted' && value.sessionID === sessionID && value.origin === 'ManagerGuard')
    let providerStarted
    if (phase !== 'STARTED') {
      await waitFor(({ kind, value }) => kind === 'guard.paused' && value.messageID === guard.value.messageID)
    } else {
      providerStarted = await waitFor(({ sequence, kind, value }) => sequence > phaseStart && kind === 'guard.provider' && value.sessionID === sessionID && value.messageID === guard.value.messageID)
      const messages = await request('GET', `/session/${sessionID}/message`)
      assert.ok(messages.some(row => row.info?.role === 'assistant' && row.info.parentID === guard.value.messageID && row.info.time?.completed === undefined), 'the held provider must belong to this Guard')
    }
    const firstHumanID = `msg_human_${phase}`
    const humanRequest = request('POST', `/session/${sessionID}/message`, prompt(firstHumanID, phase === 'INTERLEAVED' ? 'SUPERSESSION_HUMAN_WAIT' : 'SUPERSESSION_HUMAN'))
    humanRequest.catch(error => publish('fixture.failure', { error: String(error) }))
    await waitFor(({ kind, value }) => kind === 'message.accepted' && value.sessionID === sessionID && value.messageID === firstHumanID)
    if (phase !== 'STARTED') release(guard.value.messageID)
    let messageID = firstHumanID
    let answer
    if (phase === 'INTERLEAVED') {
      await waitFor(({ kind, value }) => kind === 'human.paused' && value.sessionID === sessionID && value.messageID === firstHumanID)
      messageID = `msg_newer_${phase}`
      answer = await request('POST', `/session/${sessionID}/message`, prompt(messageID, 'SUPERSESSION_HUMAN_J'))
      release(firstHumanID)
      await humanRequest
    } else answer = await humanRequest
    assert.equal(answer.info.parentID, messageID, 'HTTP success must belong to the fresh human input')
    assert.equal(answer.info.sessionID, sessionID)
    assert.equal(answer.info.finish, 'stop')
    assert.equal(answer.info.error, undefined)
    assert.ok(answer.parts.some(part => part.type === 'text' && part.text === 'HUMAN_ANSWER'))
    await waitFor(({ kind, value }) => kind === 'event.completed' && value.type === 'message.updated'
      && value.properties.info.sessionID === sessionID && value.properties.info.id === answer.info.id && value.properties.info.time?.completed !== undefined)
    if (phase !== 'STARTED') await waitFor(({ kind, value }) => kind === 'guard.late.rejected' && value.sessionID === sessionID && value.messageID === guard.value.messageID)
    else await waitFor(({ kind, value }) => kind === 'guard.provider.closed' && value.requestID === providerStarted.value.requestID)
    const serializedFacts = facts()
    const old = queryFacts(serializedFacts, sessionID, guard.value.messageID)
    assert.equal(old.ok, true, old.error)
    assert.equal(old.status.terminal, true)
    assert.equal(old.status.disposition, 'Cancelled')
    assert.equal(old.status.providerStarted, phase === 'STARTED', 'the two Guard phases must have distinct durable provider-start evidence')
    assert.equal(terminalCount(serializedFacts, sessionID, guard.value.messageID), 1, 'supersession has one durable terminal fact')
    const current = queryFacts(serializedFacts, sessionID, messageID)
    assert.equal(current.ok, true, current.error)
    assert.equal(current.status.disposition, 'Completed')
    assert.equal(terminalCount(serializedFacts, sessionID, messageID), 1)
    let intermediate
    if (phase === 'INTERLEAVED') {
      intermediate = queryFacts(serializedFacts, sessionID, firstHumanID)
      assert.equal(intermediate.ok, true, intermediate.error)
      assert.equal(intermediate.status.disposition, 'Cancelled')
      assert.equal(terminalCount(serializedFacts, sessionID, firstHumanID), 1)
    }
    assert.equal(observations.filter(({ kind, value }) => kind === 'message.received' && value.sessionID === sessionID && value.origin === 'ProviderRetryAttempt').length, 0)
    results.push({ phase, sessionID, guard: guard.value.messageID, human: messageID, old: old.status, current: current.status, intermediate: intermediate?.status, answerParent: answer.info.parentID, providerRetries: 0 })
    await request('POST', `/session/${sessionID}/abort`, {})
  }
} catch (error) {
  console.error(error)
  console.error(JSON.stringify({ observations, results, executionFacts: facts() }))
  console.error(`Host stdout:\n${host.stdoutLog}\nHost stderr:\n${host.stderrLog}`)
  const logDirectory = path.join(scenarioDir, 'xdg/data/opencode/log')
  if (fs.existsSync(logDirectory)) {
    for (const name of fs.readdirSync(logDirectory).filter(name => name.endsWith('.log'))) {
      console.error(`Host log ${name}:\n${fs.readFileSync(path.join(logDirectory, name), 'utf8').split('\n').slice(-80).join('\n')}`)
    }
  }
  process.exitCode = 1
} finally {
  for (const key of releases.keys()) release(key)
  for (const sessionID of sessions) {
    try {
      await request('POST', `/session/${sessionID}/abort`, {})
    } catch (error) {
      console.error(error)
      process.exitCode = 1
    }
  }
  try {
    await host.stop()
  } catch (error) {
    console.error(error)
    process.exitCode = 1
  }
  const cleanups = await Promise.allSettled([stopHttpServer(provider), stopHttpServer(collector)])
  for (const cleanup of cleanups) {
    if (cleanup.status === 'rejected') {
      console.error(cleanup.reason)
      process.exitCode = 1
    }
  }
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
if (!process.exitCode) process.stdout.write(`${JSON.stringify({ opencode: opencodeVersion, capacityOne, results, physicalCleanup: true })}\n`)
