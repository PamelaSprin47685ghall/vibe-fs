import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { configure, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import * as events from '../../../dist/Persistence/EventStore/Surface.js'

installDefaultResources()

test('WHAT[epistemic-reasoning-031] native sphinx command and exact tool replace the MCP launch and wildcard', () => {
  const otherServer = { type: 'remote', url: 'https://example.invalid/mcp' }
  const config = { mcp: { unrelated: otherServer } }
  assert.equal(configure(config).ok, true)
  assert.equal(Object.hasOwn(config.mcp, 'sphinx'), false)
  assert.equal(config.mcp.unrelated, otherServer)
  assert.match(config.command.sphinx.template, /sphinx/)
  assert.match(config.command.sphinx.template, /\$ARGUMENTS/)
  assert.notEqual(config.command.sphinx.subtask, true)
  for (const role of ['manager', 'orchestrator', 'engineer']) {
    assert.equal(config.agent[role].permission.sphinx, 'allow', role)
    assert.notEqual(config.agent[role].permission['sphinx_*'], 'allow', role)
  }
  for (const role of ['devops', 'blogger']) {
    assert.equal(config.agent[role].permission.sphinx, 'deny', role)
  }
  assert.equal(Object.hasOwn(config.agent, 'sphinx'), false)
  assert.equal(Object.hasOwn(config.agent, 'inquiry'), false)
})

test('WHAT[epistemic-reasoning-031] repeated configuration preserves other commands and never installs a Sphinx subagent', () => {
  const custom = { template: 'keep this command' }
  const config = { command: { custom } }
  assert.equal(configure(config).ok, true)
  const first = structuredClone(config.command.sphinx)
  assert.equal(configure(config).ok, true)
  assert.deepEqual(config.command.sphinx, first)
  assert.equal(config.command.custom, custom)
  assert.equal(Object.hasOwn(config.mcp ?? {}, 'sphinx'), false)
})

async function inquiryFixture(t) {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'sphinx-native-'))
  const api = await import('../../../dist/Sphinx/InquirySurface.js')
  const handles = []
  const open = () => {
    const handle = events.create(directory, `writer-${handles.length}`)
    handles.push(handle)
    return api.createRuntime(handle)
  }
  t.after(async () => {
    for (const handle of handles) events.dispose(handle)
    await fs.rm(directory, { recursive: true, force: true })
  })
  return { api, open }
}

const emptyInquiryObservation = ({ request }) => {
  switch (request.type) {
    case 'SemanticAssessmentRequest': return { type: 'SemanticAssessment', forms: { What: 1 } }
    case 'GenerateCandidatesRequest': return { type: 'Candidates', items: [] }
    default: throw new Error(`Unexpected request: ${request.type}`)
  }
}

test('WHAT[epistemic-reasoning-031] one invocation consumes all pending requests and exposes no continuation protocol', async t => {
  const { api, open } = await inquiryFixture(t)
  const seen = []
  const result = await api.run(open(), 'one-call', 'What can be established?', async work => {
    seen.push(work.request.type)
    return emptyInquiryObservation(work)
  }, () => false)
  assert.deepEqual(seen, ['SemanticAssessmentRequest', 'GenerateCandidatesRequest'])
  assert.equal(result.status, 'answered')
  assert.equal(result.answer.question, 'What can be established?')
  assert.equal(Object.hasOwn(result, 'nextTool'), false)
  assert.equal(Object.hasOwn(result, 'request'), false)
  assert.equal(result.answer.epistemicBasis.evidence.length, 0)
})

test('WHAT[epistemic-reasoning-033] accepted work is not purchased again after runtime restart', async t => {
  const { api, open } = await inquiryFixture(t)
  let purchases = 0
  const observe = async work => { purchases++; return emptyInquiryObservation(work) }
  const first = await api.run(open(), 'durable-call', 'Question', observe, () => false)
  assert.equal(purchases, 2)
  const replayed = await api.run(open(), 'durable-call', 'Question', observe, () => false)
  assert.deepEqual(replayed, first)
  assert.equal(purchases, 2)
})

test('WHAT[epistemic-reasoning-033] concurrent retries share one invocation and reject identity reuse for another question', async t => {
  const { api, open } = await inquiryFixture(t)
  const runtime = open()
  let purchases = 0
  const observe = async work => { purchases++; await Promise.resolve(); return emptyInquiryObservation(work) }
  const [first, retry] = await Promise.all([
    api.run(runtime, 'same-call', 'Question', observe, () => false),
    api.run(runtime, 'same-call', 'Question', observe, () => false),
  ])
  assert.deepEqual(retry, first)
  assert.equal(purchases, 2)
  await assert.rejects(api.run(runtime, 'same-call', 'Different question', observe, () => false), /question|identity/i)
  assert.equal(purchases, 2)
})

test('WHAT[epistemic-reasoning-034] cancellation fences an in-flight observation and remains terminal on retry', async t => {
  const { api, open } = await inquiryFixture(t)
  let cancelled = false
  let release
  let started
  const entered = new Promise(resolve => { started = resolve })
  const pending = api.run(open(), 'cancelled-call', 'Question', work => {
    started()
    return new Promise(resolve => { release = () => resolve(emptyInquiryObservation(work)) })
  }, () => cancelled)
  await entered
  cancelled = true
  release()
  const result = await pending
  assert.equal(result.status, 'unresolved')
  assert.equal(result.answer.stopReason, 'cancelled')
  assert.equal(result.answer.revision, 0, 'late observation must not advance the kernel')
  const replayed = await api.run(open(), 'cancelled-call', 'Question', () => {
    assert.fail('a cancelled invocation must not repurchase work')
  }, () => false)
  assert.deepEqual(replayed, result)
})

test('WHAT[epistemic-reasoning-004] an observation for the wrong phase is not absorbed', async t => {
  const { api, open } = await inquiryFixture(t)
  const result = await api.run(open(), 'wrong-phase', 'Question', async () => ({ type: 'Candidates', items: [] }), () => false)
  assert.equal(result.status, 'unresolved')
  assert.match(result.answer.stopReason, /invalid-observation/)
  assert.equal(result.answer.revision, 0)
  assert.equal(result.answer.epistemicBasis.evidence.length, 0)
})

test('WHAT[epistemic-reasoning-013] an empty question never purchases model work', async t => {
  const { api, open } = await inquiryFixture(t)
  await assert.rejects(api.run(open(), 'empty-question', '   ', () => {
    assert.fail('empty questions must fail before research')
  }, () => false), /question/i)
})
