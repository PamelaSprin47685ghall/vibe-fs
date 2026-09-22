import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { tool } from '@opencode-ai/plugin'
import * as events from '../../../dist/Persistence/EventStore/Surface.js'
import { installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { before as commandBefore } from '../../../dist/OpenCode/Plugin/SphinxCommandSurface.js'

installDefaultResources()

const answer = {
  question: 'Question', synthesis: { text: 'Verified answer', findingKeys: ['fact'] },
  epistemicBasis: { findings: [], evidence: [], hypotheses: [] },
  uncertainties: ['Scope is limited'], stopReason: 'stop-dominates', revision: 5,
}

function commandClient() {
  const prompts = []
  return { prompts, client: { session: { prompt: async payload => {
    prompts.push(payload)
    return { data: { info: { role: 'user' }, parts: payload.body.parts } }
  } } } }
}

test('WHAT[epistemic-reasoning-035] slash command directly executes and inserts question plus answer with noReply', async () => {
  const { client, prompts } = commandClient()
  const calls = []
  const output = { parts: [{ type: 'text', text: 'old template' }], handled: false }
  const question = '为什么这样实现？\n请核对实际代码。'
  const result = await commandBefore(async (...args) => { calls.push(args); return answer }, client, '/workspace',
    { command: 'sphinx', sessionID: 'current-sub-session', arguments: `--expect-turns 5 ${question}` }, output)
  assert.equal(result, true)
  assert.deepEqual(calls, [['current-sub-session', question, 5]])
  assert.equal(output.handled, true)
  assert.deepEqual(output.parts, [])
  assert.equal(prompts.length, 1)
  assert.equal(prompts[0].body.noReply, true)
  assert.equal(prompts[0].noReply, true)
  assert.equal(prompts[0].path.id, 'current-sub-session')
  assert.equal(prompts[0].body.parts.length, 1)
  assert.ok(prompts[0].body.parts[0].text.includes(question))
  assert.ok(prompts[0].body.parts[0].text.includes('Verified answer'))
  assert.ok(prompts[0].body.parts[0].text.includes('Scope is limited'))
})

test('WHAT[epistemic-reasoning-035] old hosts are explicitly stopped after noReply insertion instead of falling through', async () => {
  const { client, prompts } = commandClient()
  await assert.rejects(commandBefore(async () => answer, client, undefined,
    { command: 'sphinx', sessionID: 'current', arguments: 'Question' }, { parts: [] }),
  error => error.name === 'SphinxCommandHandled')
  assert.equal(prompts.length, 1)
  assert.equal(prompts[0].body.noReply, true)
})

test('WHAT[epistemic-reasoning-035] unrelated commands and invalid arguments produce no Sphinx effects', async () => {
  const { client, prompts } = commandClient()
  const run = () => assert.fail('must not execute Sphinx')
  const output = { parts: ['unchanged'], handled: false }
  assert.equal(await commandBefore(run, client, undefined,
    { command: 'continue', sessionID: 'current', arguments: 'anything' }, output), false)
  assert.deepEqual(output.parts, ['unchanged'])
  for (const argumentsText of ['', '  ', '--expect-turns 0 Question', '--expect-turns -1 Question',
    '--expect-turns 1.5 Question', '--expect-turns abc Question', '--expect-turns 2147483648 Question', '--expect-turns 2']) {
    await assert.rejects(commandBefore(run, client, undefined,
      { command: 'sphinx', sessionID: 'current', arguments: argumentsText }, output))
  }
  assert.deepEqual(prompts, [])
})

function workFromCharge(charge) {
  const marker = 'Current work and known basis:\n'
  return JSON.parse(charge.slice(charge.lastIndexOf(marker) + marker.length))
}

test('WHAT[epistemic-reasoning-035] unavailable execution rejects before inserting material or resuming the command', async () => {
  const { client, prompts } = commandClient()
  const output = { parts: [{ type: 'text', text: 'inert template' }], handled: false }
  await assert.rejects(commandBefore(async () => {
    throw new Error('Sphinx requires the managed Engineer runtime and workspace EventStore')
  }, client, undefined, { command: 'sphinx', sessionID: 'current', arguments: 'Question' }, output),
  /managed Engineer runtime/)
  assert.deepEqual(prompts, [])
  assert.equal(output.handled, false)
})

function observation(work) {
  const { request, basis } = work
  switch (request.type) {
    case 'SemanticAssessmentRequest': return { type: 'SemanticAssessment', forms: { Why: 1 }, facets: { causal: 1, explanatory: 1 } }
    case 'GenerateCandidatesRequest': return { type: 'Candidates', items: basis.epistemicBasis.findings.length ? [] : [{
      method: 'CausalMechanism', question: 'Investigate mechanism', semanticKey: 'mechanism', expectedRootGain: 0.95, cost: 0.1,
    }] }
    case 'InvestigateRequest': return { type: 'Investigation', actionKey: request.action.id,
      findings: [{ semanticKey: 'fact', text: 'Verified fact', evidenceKeys: ['source'] }],
      evidence: [{ semanticKey: 'source', proposition: 'Observed source', source: { id: 'test-source', kind: 'tool' }, dependencyKey: 'test-source' }] }
    case 'SynthesizeRequest': return { type: 'Synthesis', text: 'Verified answer', findingKeys: ['fact'], uncertainties: [] }
    default: assert.fail(`Unexpected request: ${request.type}`)
  }
}

async function fixture(t, invoke, cancel = async () => {}) {
  const api = await import('../../../dist/OpenCode/Host/SphinxExecutionSurface.js')
  const directory = await mkdtemp(path.join(tmpdir(), 'sphinx-execution-'))
  const store = events.create(directory, 'sphinx-test')
  const execution = api.create(store, invoke, cancel)
  t.after(async () => {
    await api.dispose(execution)
    events.dispose(store)
    await rm(directory, { recursive: true, force: true })
  })
  return { api, execution }
}

for (const expected of [5, 100]) {
  test(`WHAT[epistemic-reasoning-035] expectTurns=${expected} guides prompts but neither truncates nor pads the inquiry`, async t => {
    const charges = []
    const { api, execution } = await fixture(t, async invocation => {
      charges.push(invocation.charge)
      assert.equal(invocation.ownerSessionId, 'current-sub-session')
      invocation.admitted('ordinary-engineer')
      return JSON.stringify(observation(workFromCharge(invocation.charge)))
    })
    const result = await api.run(execution, 'current-sub-session', `expected-${expected}`, 'Why?', expected, () => () => {})
    assert.equal(result.synthesis.text, 'Verified answer')
    assert.equal(result.epistemicBasis.evidence.length, 1)
    assert.equal(Object.hasOwn(result, 'nextTool'), false)
    assert.equal(Object.hasOwn(result, 'request'), false)
    assert.equal(charges.length, 5)
    assert.ok(charges.every(charge => charge.includes(`approximately ${expected} inquiry turns`)))
  })
}

test('WHAT[epistemic-reasoning-035] native tool has only question and expectTurns and returns answer directly', async t => {
  let purchases = 0
  const { api, execution } = await fixture(t, async invocation => {
    purchases++
    invocation.admitted('engineer')
    return JSON.stringify(observation(workFromCharge(invocation.charge)))
  })
  const spec = api.tool(execution, { tool })
  assert.deepEqual(Object.keys(spec.args).sort(), ['expectTurns', 'question'])
  const context = { sessionID: 'current', messageID: 'current-provider-run', callID: 'same-call', agent: 'engineer', abort: new AbortController().signal }
  for (const expectTurns of [0, 1, 4, -1, 1.5, 512]) {
    assert.equal(spec.args.expectTurns.safeParse(expectTurns).success, false)
    await assert.rejects(spec.execute({ question: 'Why?', expectTurns }, context), /expectTurns/)
  }
  for (const expectTurns of [undefined, 5, 100, 511]) {
    assert.equal(spec.args.expectTurns.safeParse(expectTurns).success, true)
  }
  assert.equal(purchases, 0)
  const first = JSON.parse(await spec.execute({ question: 'Why?', expectTurns: 5 }, context))
  assert.equal(first.synthesis.text, 'Verified answer')
  assert.equal(first.answer, undefined, 'the result is answer, not a phase/status wrapper')
  const retry = JSON.parse(await spec.execute({ question: 'Why?', expectTurns: 5 }, context))
  assert.deepEqual(retry, first)
  assert.equal(purchases, 5)
})

test('WHAT[epistemic-reasoning-035] cancellation rejects late observations and awaits Engineer drain', { timeout: 10_000 }, async t => {
  let entered, release, drained, cancelCall, cancelEntered
  const started = new Promise(resolve => { entered = resolve })
  const drain = new Promise(resolve => { drained = resolve })
  const cancelling = new Promise(resolve => { cancelEntered = resolve })
  const cancelledChildren = []
  const { api, execution } = await fixture(t, invocation => {
    invocation.admitted('exact-engineer')
    entered()
    return new Promise(resolve => { release = () => resolve(JSON.stringify(observation(workFromCharge(invocation.charge)))) })
  }, async child => { cancelledChildren.push(child); cancelEntered(); await drain })
  let detached = 0
  const pending = api.run(execution, 'owner', 'cancelled', 'Why?', undefined, cancel => {
    cancelCall = cancel
    return () => detached++
  })
  await started
  cancelCall()
  release()
  await cancelling
  assert.deepEqual(cancelledChildren, ['exact-engineer'])
  drained()
  const result = await pending
  assert.equal(result.stopReason, 'cancelled')
  assert.equal(result.revision, 0)
  assert.equal(result.epistemicBasis.evidence.length, 0)
  assert.equal(detached, 1)
})

test('WHAT[epistemic-reasoning-035] real OpenCode command writes one user message and makes zero provider requests', { timeout: 120_000 }, async t => {
  const { ProcessHost } = await import('../../verification-system/tests/e2e/support/process-host.js')
  const { initGitWorkspace } = await import('../../verification-system/tests/e2e/support/process-host-utils.js')
  const { startHttpServer, stopHttpServer, readRequestBody } = await import('../../verification-system/tests/e2e/support/strict-mock-server.js')
  const { sendJSON, sendSSE, buildTextChunks } = await import('../../verification-system/tests/e2e/support/strict-mock-sse.js')
  const { mkdir } = await import('node:fs/promises')
  const { fileURLToPath } = await import('node:url')
  const requests = []
  const provider = await startHttpServer(async (request, response) => {
    const url = new URL(request.url, `http://${request.headers.host}`)
    if (request.method === 'GET' && url.pathname.endsWith('/models')) {
      return sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] })
    }
    if (request.method === 'POST' && url.pathname.endsWith('/chat/completions')) {
      requests.push(await readRequestBody(request))
      return sendSSE(response, buildTextChunks('unexpected-parent-turn', 'UNEXPECTED_PROVIDER_TURN', 1))
    }
    sendJSON(response, 404, { error: 'unexpected endpoint' })
  })
  const scenarioDir = await mkdtemp(path.join(tmpdir(), 'sphinx-command-host-'))
  const workspace = path.join(scenarioDir, 'workspace')
  await mkdir(workspace, { recursive: true })
  await initGitWorkspace(workspace)
  const host = new ProcessHost()
  t.after(async () => {
    try { await host.stop() } finally {
      await stopHttpServer(provider.server)
      await rm(scenarioDir, { recursive: true, force: true })
    }
  })
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [fileURLToPath(new URL('./support/sphinx-command-canary.mjs', import.meta.url))],
  })
  const request = async (method, pathname, body) => {
    const response = await fetch(host.baseUrl + pathname, {
      method, headers: { 'content-type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    const text = await response.text()
    return { status: response.status, data: text ? JSON.parse(text) : null }
  }
  const created = await request('POST', '/api/session', { agent: 'engineer', model: { providerID: 'test', id: 'test-model' } })
  assert.equal(created.status, 200)
  const sid = created.data?.data?.data?.id ?? created.data?.data?.id ?? created.data?.id
  assert.ok(sid)
  const result = await request('POST', `/session/${sid}/command`, {
    command: 'sphinx', arguments: 'Real Host question', agent: 'engineer', model: 'test/test-model',
  })
  const stored = await request('GET', `/session/${sid}/message`)
  assert.equal(stored.status, 200)
  const messages = Array.isArray(stored.data) ? stored.data : (stored.data?.data ?? [])
  assert.equal(messages.length, 1, JSON.stringify({ result, messages }))
  assert.equal(messages[0].info.role, 'user')
  const text = messages[0].parts.filter(part => part.type === 'text').map(part => part.text).join('\n')
  assert.ok(text.includes('Real Host question'), text)
  assert.ok(text.includes('SPHINX_NATIVE_ANSWER'), text)
  assert.equal(requests.length, 0, 'no provider turn is allowed after command completion')
  t.diagnostic(`Real Host command response HTTP ${result.status}; stored one user message and made zero provider requests`)
})
