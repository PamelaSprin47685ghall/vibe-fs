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
let subOwnerStep = 0
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
      fs.writeFileSync(path.join(workspace, 'fixture.txt'), `readonly-evidence-${replicaStep}`)
      sendSSE(res, buildToolCallChunks(`replica-${replicaStep}`, 'js-predictor', JSON.stringify({ program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }" }), 10))
    } else {
      sendSSE(res, buildTextChunks(`replica-done-${replicaStep}`, 'The evidence is sufficient.', 20))
    }
    return
  }
  if (names.includes('js-engineer')) {
    subOwnerStep += 1
    if (subOwnerStep === 1) {
      sendSSE(res, buildToolCallChunks('owner-sub', 'js-engineer', JSON.stringify({
        program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }",
        estimated_readonly_rounds: 2,
        self_note: 'Inspect the fixture and stop once its contents are verified.',
      }), 10))
    } else {
      sendSSE(res, buildTextChunks('owner-done-sub-owner', 'Sub-owner smoke complete.', 20))
    }
    return
  }
  if (names.includes('js-manager')) {
    ownerStep += 1
    if (ownerStep <= 2) {
      sendSSE(res, buildToolCallChunks(`owner-${ownerStep}`, 'js-manager', JSON.stringify({ program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review', estimated_readonly_rounds: 2, self_note: 'Inspect the fixture and stop once its contents are verified.' }), 10))
    } else if (ownerStep === 3) {
      sendSSE(res, buildTextChunks('owner-done', 'Smoke complete.', 20))
    } else if (ownerStep === 4) {
      sendSSE(res, buildToolCallChunks(`owner-${ownerStep}`, 'js-manager', JSON.stringify({ program: "class Js extends JsProgram { async run() { const f = await this.file('fixture.txt'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review', estimated_readonly_rounds: 2, self_note: 'Inspect the fixture third time post restart.' }), 10))
    } else {
      sendSSE(res, buildTextChunks('owner-done-post-restart', 'Restart smoke complete.', 20))
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
let currentHost = new ProcessHost()
const request = async (hostInstance, method, pathname, body) => {
  const response = await fetch(hostInstance.baseUrl + pathname, { method, headers: { 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await response.text()
  assert.ok(response.ok, `${method} ${pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : null
}
let session
let subOwner
let timer
const promptOwner = async (messageID, text, targetSession = session, agent = 'manager') => {
  try {
    return await Promise.race([
      request(currentHost, 'POST', `/session/${targetSession}/message`, {
        messageID,
        agent,
        model: { providerID: 'test', modelID: 'test-model' },
        parts: [{ type: 'text', text }],
      }),
      new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Owner assignment did not complete')), 45000) }),
    ])
  } finally {
    clearTimeout(timer)
  }
}
try {
  await currentHost.start({ scenarioDir, providerUrl: `http://127.0.0.1:${provider.address().port}/v1`, pluginPaths: [path.join(root, 'dist/OpenCode/Plugin/Plugin.js')], routingSource: `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport const predictorConfiguration = () => ({ state: 'configured', reason: null })\nexport default function route() { return { model: 'test/test-model', reasoning: 'none' } }\n` })
  const created = await request(currentHost, 'POST', '/session', { title: 'resident predictor canary' })
  session = created.id
  assert.ok(session)
  const first = await promptOwner('msg_assignment_smoke', 'Review the fixture, checking it in two successive investigation batches.')
  assert.ok(first.info.time.completed)
  assert.ok(first.parts.some(part => part.text === 'Smoke complete.'))
  assert.equal(replicaRequests.length, 4)
  const childrenPayload1 = await request(currentHost, 'GET', `/session/${session}/children`)
  const residents = childrenPayload1.filter(child => child.permission?.some(rule => rule.permission === 'js-predictor' && rule.action === 'allow'))
  assert.equal(residents.length, 1, 'one readonly resident before restart')
  const residentChildId = residents[0].id
  assert.equal(residents[0].parentID, session, 'predictor physical parent is the main session')

  // Process restart: stop host, launch new host with same scenarioDir/storage
  await currentHost.stop()
  currentHost = new ProcessHost()
  await currentHost.start({ scenarioDir, providerUrl: `http://127.0.0.1:${provider.address().port}/v1`, pluginPaths: [path.join(root, 'dist/OpenCode/Plugin/Plugin.js')], routingSource: `export const routingProtocol = 2\nexport const hasTheoreticalCapacity = () => true\nexport const predictorConfiguration = () => ({ state: 'configured', reason: null })\nexport default function route() { return { model: 'test/test-model', reasoning: 'none' } }\n` })

  // Send third assignment to same main session post restart
  const restarted = await promptOwner('msg_assignment_restart', 'Review the fixture a third time post restart.')
  assert.ok(restarted.info.time.completed)
  assert.ok(restarted.parts.some(part => part.text === 'Restart smoke complete.'))

  assert.equal(replicaRequests.length, 6, 'exactly 6 predictor calls across 3 decisions')
  for (const body of replicaRequests) {
    assert.deepEqual(body.tools.map(tool => tool.function?.name ?? tool.name), ['js-predictor'])
    assert.ok(body.messages.some(message => message.role === 'system' && JSON.stringify(message.content).includes(instruction)), JSON.stringify(body.messages.filter(message => message.role === 'system' || message.role === 'developer')))
  }
  const childrenPayload = await request(currentHost, 'GET', `/session/${session}/children`)
  const children = childrenPayload
  const transcripts = []
  for (const child of children) {
    const messagesPayload = await request(currentHost, 'GET', `/session/${child.id}/message`)
    const messages = messagesPayload
    const prompts = messages.filter(message => message.info?.role === 'user').flatMap(message => message.parts ?? []).filter(part => part.type === 'text').map(part => part.text)
    if (prompts.includes(instruction)) transcripts.push({ child: child.id, prompts, messages })
  }
  assert.equal(transcripts.length, 1, 'one resident predictor child reused across restart')
  assert.equal(transcripts[0].child, residentChildId, 'child ID must match across process restart')
  assert.deepEqual(transcripts[0].prompts, [instruction, instruction, instruction])
  assert.doesNotMatch(currentHost.stderrLog, /ActiveRunIdentityConflict|prompt_async failed/)

  const eventFiles = fs.readdirSync(path.join(workspace, '.git/wanxiang/events'))
  const events = eventFiles.flatMap(file => fs.readFileSync(path.join(workspace, '.git/wanxiang/events', file), 'utf8').trim().split('\n').map(line => JSON.parse(line)))
  const bound = events.filter(event => event.event_type === 'DelegationBound')
  assert.equal(bound.length, 3, 'exactly 3 Bound decisions')

  const prepared = events.filter(event => event.event_type === 'StrengthCandidatePrepared')
  assert.equal(prepared.length, 3, 'exactly 3 Prepared decisions')

  const requested = events.filter(event => event.event_type === 'DelegationRequested')
  assert.equal(requested.length, 3, 'each real source batch authorizes one decision')
  const byDecision = new Map(requested.map(row => [row.payload.decision_id, row.payload]))
  const ownerMessages = await request(currentHost, 'GET', `/session/${session}/message`)
  const payloadsDir = path.join(workspace, '.git/wanxiang/payloads')
  for (const row of prepared) {
    const authorization = byDecision.get(row.payload.decision_id)
    assert.ok(authorization, 'Prepared has durable authorization')
    const target = bound.find(item => item.payload.decision_id === authorization.decision_id)
    assert.notEqual(authorization.source_provider_run, target.payload.target_provider_run)
    const source = ownerMessages.find(message => message.info?.id === authorization.source_provider_run)
    assert.ok(source, 'source is a real stored assistant, not the outgoing target placeholder')
    assert.equal(source.info.parentID, authorization.source_physical_user_message_id, 'source authorization belongs to its actual physical input')
    assert.ok(source.parts.some(part => part.callID === authorization.source_tool_call_ids[0]))
    const ordinal = new Map([['owner-1', 1], ['owner-2', 3], ['owner-4', 5]]).get(authorization.source_tool_call_ids[0])
    assert.ok(ordinal, 'known source batch')
    const exchange = transcripts[0].messages.flatMap(message => message.parts).find(part => part.callID === `replica-${ordinal}`)
    assert.equal(exchange.state.status, 'completed')
    const expected = exchange.state.output
    assert.match(expected, new RegExp(`readonly-evidence-${ordinal}`))
    for (const other of [1, 3, 5].filter(value => value !== ordinal)) {
      assert.doesNotMatch(expected, new RegExp(`readonly-evidence-${other}`))
    }
    assert.equal(row.payload_refs.length, 1)
    const ref = row.payload_refs[0]
    assert.match(ref, /^[a-f0-9]{64}$/)
    const bundle = JSON.parse(fs.readFileSync(path.join(payloadsDir, ref), 'utf8'))
    assert.equal(bundle.version, 1)
    assert.deepEqual(bundle.batches.map(batch => ({
      ordinal: batch.request_ordinal,
      results: batch.exchanges.map(exchange => exchange.result),
    })), [{ ordinal: 1, results: [expected] }], 'Prepared contains only evidence from this decision')
  }

  const createdSubOwner = await request(currentHost, 'POST', '/session', {
    parentID: session,
    title: 'restored physical sub-owner canary',
  })
  subOwner = createdSubOwner.id
  assert.equal(createdSubOwner.parentID, session, 'logical owner is an actual physical child')
  const subResult = await promptOwner('msg_assignment_sub_owner', 'Review the fixture from this sub-session.', subOwner, 'engineer')
  assert.ok(subResult.info.time.completed)
  assert.ok(subResult.parts.some(part => part.text === 'Sub-owner smoke complete.'))
  const finalEvents = fs.readdirSync(path.join(workspace, '.git/wanxiang/events')).flatMap(file =>
    fs.readFileSync(path.join(workspace, '.git/wanxiang/events', file), 'utf8').trim().split('\n').map(line => JSON.parse(line)),
  )
  const subRequested = finalEvents.filter(row =>
    row.event_type === 'DelegationRequested' && row.payload.owner_session_id === subOwner,
  )
  assert.equal(subRequested.length, 1)
  const subBinding = finalEvents.filter(row =>
    row.event_type === 'DelegationBound' && row.payload.decision_id === subRequested[0].payload.decision_id,
  )
  assert.equal(subBinding.length, 1)
  const subReplica = subBinding[0].payload.replica_session_id
  assert.notEqual(subReplica, residentChildId, 'logical owners do not share one predictor')
  const subMessages = await request(currentHost, 'GET', `/session/${subOwner}/message`)
  const subSource = subMessages.find(message => message.info.id === subRequested[0].payload.source_provider_run)
  assert.ok(subSource)
  assert.equal(subSource.info.parentID, 'msg_assignment_sub_owner')
  assert.deepEqual(subRequested[0].payload.source_tool_call_ids, ['owner-sub'])
  assert.notEqual(subSource.info.id, subBinding[0].payload.target_provider_run)
  const subTranscript = await request(currentHost, 'GET', `/session/${subReplica}/message`)
  assert.deepEqual(
    subTranscript.filter(message => message.info.role === 'user').flatMap(message => message.parts).map(part => part.text),
    [instruction],
  )
  const subExchange = subTranscript.flatMap(message => message.parts).find(part => part.callID === 'replica-7')
  assert.equal(subExchange.state.status, 'completed')
  assert.match(subExchange.state.output, /readonly-evidence-7/)
  const subPrepared = finalEvents.filter(row =>
    row.event_type === 'StrengthCandidatePrepared' && row.payload.decision_id === subRequested[0].payload.decision_id,
  )
  assert.equal(subPrepared.length, 1)
  assert.equal(subPrepared[0].payload_refs.length, 1)
  const subBundle = JSON.parse(fs.readFileSync(path.join(payloadsDir, subPrepared[0].payload_refs[0]), 'utf8'))
  assert.deepEqual(subBundle.batches.map(batch => batch.exchanges.map(exchange => exchange.result)), [[subExchange.state.output]])
  for (const body of replicaRequests.slice(6)) {
    assert.deepEqual(body.tools.map(tool => tool.function?.name ?? tool.name), ['js-predictor'])
  }
  const physicalFamily = await request(currentHost, 'GET', `/session/${session}/children`)
  assert.ok(physicalFamily.some(child => child.id === subReplica && child.parentID === session))
  const subChildren = await request(currentHost, 'GET', `/session/${subOwner}/children`)
  assert.deepEqual(subChildren, [], 'managed companions of the sub-owner are physically flattened to the family root')
  assert.equal(finalEvents.filter(row => row.event_type === 'DelegationRequested').length, 4)
  assert.equal(finalEvents.filter(row => row.event_type === 'DelegationBound').length, 4)
  assert.equal(finalEvents.filter(row => row.event_type === 'StrengthCandidatePrepared').length, 4)
  assert.equal(replicaRequests.length, 8)

  console.log(`RESIDENT_PREDICTOR_CANARY ${JSON.stringify({ managerAssignments: 3, subOwnerAssignments: 1, predictorRequests: replicaRequests.length, residentChild: transcripts[0].child, readableBootstrapMessages: transcripts[0].prompts.length, boundDecisions: 4, preparedDecisions: 4, bareContinueMessages: 0, predictorTools: ['js-predictor'], restarted: true, sourceIdentityVerified: true, decisionMaterialIsolated: true, subOwnerFlattened: true })}`)
} catch (error) {
  console.error(currentHost.stdoutLog.slice(-5000))
  console.error(currentHost.stderrLog.slice(-5000))
  throw error
} finally {
  clearTimeout(timer)
  if (subOwner && currentHost.baseUrl) {
    try { await request(currentHost, 'POST', `/session/${subOwner}/abort`, {}) } catch {}
  }
  if (session && currentHost.baseUrl) {
    try { await request(currentHost, 'POST', `/session/${session}/abort`, {}) } catch {}
  }
  await currentHost.stop()
  await new Promise(resolve => provider.close(resolve))
  fs.rmSync(scenarioDir, { recursive: true, force: true })
}
