import assert from 'node:assert/strict'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js'
import { initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js'
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js'
const root = path.resolve(import.meta.dirname, '../../../..')
const instruction = fs.readFileSync(path.join(root, 'resources/provider/delegation/readonly-investigation/en.md'), 'utf8').trim()
const replicaRequests = []
let ownerStep = 0
let replicaStep = 0
let resolveFinished
const finished = new Promise(resolve => { resolveFinished = resolve })
const provider = http.createServer(async (req, res) => {
  if (req.method === 'GET') {
    sendJSON(res, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    return
  }
  const chunks = []
  for await (const chunk of req) chunks.push(chunk)
  const body = JSON.parse(Buffer.concat(chunks).toString())
  const names = (body.tools ?? []).map(tool => tool.function?.name ?? tool.name)
  if (names.includes('js-predictor')) {
    replicaRequests.push(body)
    replicaStep += 1
    if (replicaStep % 2 === 1) {
      sendSSE(res, buildToolCallChunks(`replica-${replicaStep}`, 'js-predictor', JSON.stringify({ program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }" }), 10))
    } else {
      sendSSE(res, buildTextChunks(`replica-done-${replicaStep}`, 'The evidence is sufficient.', 20))
    }
    return
  }
  if (names.includes('js-manager')) {
    ownerStep += 1
    if (ownerStep <= 2) {
      sendSSE(res, buildToolCallChunks(`owner-${ownerStep}`, 'js-manager', JSON.stringify({ program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review', estimated_readonly_rounds: 2, self_note: 'Inspect the fixture and stop once its contents are verified.' }), 10))
    } else {
      sendSSE(res, buildTextChunks('owner-done', 'Smoke complete.', 20))
      resolveFinished()
    }
    return
  }
  sendSSE(res, buildTextChunks('aux', 'Auxiliary response.', 20))
})
await new Promise(resolve => provider.listen(0, '127.0.0.1', resolve))
const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-assignment-smoke-'))
const workspace = path.join(scenarioDir, 'workspace')
fs.mkdirSync(workspace)
fs.writeFileSync(path.join(workspace, 'fixture.txt'), 'verified fixture evidence\n')
await initGitWorkspace(workspace)
const host = new ProcessHost()
const request = async (method, pathname, body) => {
  const response = await fetch(host.baseUrl + pathname, { method, headers: { 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text()
  assert.ok(response.ok, `${method} ${pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
let session
let timer
try {
  await host.start({ scenarioDir, providerUrl: `http://127.0.0.1:${provider.address().port}/v1`, pluginPaths: [path.join(root, 'dist/OpenCode/Plugin/Plugin.js')], routingSource: `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport const predictorConfiguration = () => ({ state: 'configured', reason: null })\nexport default function route() { return { model: 'test/test-model', reasoning: 'none' } }\n` })
  const created = await request('POST', '/api/session', { agent: 'manager', model: { providerID: 'test', id: 'test-model' } })
  session = created?.data?.data?.data?.id ?? created?.data?.data?.id ?? created?.data?.id ?? created?.id
  assert.ok(session)
  await request('POST', `/session/${session}/prompt_async`, { messageID: 'msg_assignment_smoke', agent: 'manager', model: { providerID: 'test', modelID: 'test-model' }, parts: [{ type: 'text', text: 'Review the fixture, checking it in two successive investigation batches.' }] })
  await Promise.race([finished, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Manager did not complete two assignments')), 45000) })])
  assert.equal(replicaRequests.length, 4)
  for (const body of replicaRequests) {
    assert.deepEqual(body.tools.map(tool => tool.function?.name ?? tool.name), ['js-predictor'])
    assert.ok(body.messages.some(message => message.role === 'system' && JSON.stringify(message.content).includes(instruction)), JSON.stringify(body.messages.filter(message => message.role === 'system' || message.role === 'developer')))
  }
  const childrenPayload = await request('GET', `/session/${session}/children`)
  const children = Array.isArray(childrenPayload) ? childrenPayload : childrenPayload.data
  const transcripts = []
  for (const child of children) {
    const messagesPayload = await request('GET', `/session/${child.id}/message`)
    const messages = Array.isArray(messagesPayload) ? messagesPayload : messagesPayload.data
    const prompts = messages.filter(message => message.info?.role === 'user').flatMap(message => message.parts ?? []).filter(part => part.type === 'text').map(part => part.text)
    if (prompts.includes(instruction)) transcripts.push({ child: child.id, prompts })
  }
  assert.equal(transcripts.length, 1, 'one resident predictor child')
  assert.deepEqual(transcripts[0].prompts, [instruction, instruction])
  assert.doesNotMatch(host.stderrLog, /ActiveRunIdentityConflict|prompt_async failed/)
  const eventFiles = fs.readdirSync(path.join(workspace, '.git/wanxiang/events'))
  const events = eventFiles.flatMap(file => fs.readFileSync(path.join(workspace, '.git/wanxiang/events', file), 'utf8').trim().split('\n').map(line => JSON.parse(line)))
  const bound = events.filter(event => event.event_type === 'DelegationBound')
  assert.equal(bound.length, 2)
  console.log(`RESIDENT_PREDICTOR_CANARY ${JSON.stringify({ managerAssignments: ownerStep - 1, predictorRequests: replicaRequests.length, residentChild: transcripts[0].child, readableBootstrapMessages: transcripts[0].prompts.length, boundDecisions: bound.length, bareContinueMessages: 0, predictorTools: ['js-predictor'] })}`)
} catch (error) {
  console.error(host.stdoutLog.slice(-5000))
  console.error(host.stderrLog.slice(-5000))
  throw error
} finally {
  clearTimeout(timer)
  if (session && host.baseUrl) {
    try { await request('POST', `/session/${session}/abort`, {}) } catch {}
  }
  await host.stop()
  await new Promise(resolve => provider.close(resolve))
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
