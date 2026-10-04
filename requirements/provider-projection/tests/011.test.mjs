import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFile } from 'node:child_process'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as Journal from '../../../dist/Persistence/Journal/Surface.js'
import * as Enforcer from '../../../dist/Enforcer/Surface.js'
import * as Dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as Trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { acceptAuthorityRoot, awaitPrompted, configureManagedPlugin, withRestartablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const sha256 = (text) => createHash('sha256').update(text, 'utf8').digest('hex')
const journalFacts = (directory) => readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
  const path = join(directory, entry.name)
  if (entry.isDirectory()) return journalFacts(path)
  if (!entry.name.endsWith('.ndjson')) return []
  return readFileSync(path, 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line))
})
const payloadFor = (value, name) => {
  if (Array.isArray(value) && value[0] === name) return value[1]
  if (value !== null && typeof value === 'object') {
    for (const nested of Object.values(value)) {
      const payload = payloadFor(nested, name)
      if (payload !== undefined) return payload
    }
  }
}
const projection = (callId, args = '{"path":"x"}') => ({
  providerId: null, modelId: null, variant: null, tools: [], system: [],
  messages: [{ role: 'assistant', parts: [{ kind: 'tool-call', callId, name: 'read', args }] }],
})

test('WHAT[provider-projection-011] semantic digest is invariant to physical call IDs but sensitive to actual arguments', () => {
  const first = projection('call-first')
  const second = projection('call-second')
  const changed = projection('call-third', '{"path":"y"}')
  assert.equal(Projection.semanticallyEqual(first, second), true)
  assert.equal(Projection.semanticallyEqual(first, changed), false)
  assert.notEqual(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
  const digest = (value) => Projection.cutoffDigest(sha256, Projection.projectionSnapshot(Projection.semanticProjection(value.messages)), 1)
  assert.equal(digest(first), sha256(Projection.renderSemantic(first)))
  assert.match(digest(first), /^[0-9a-f]{64}$/)
  assert.equal(digest(first), digest(second))
  assert.notEqual(digest(first), digest(changed))
})

test('WHAT[provider-projection-011] cutoff digest includes exactly the selected semantic prefix', () => {
  const messages = ['first', 'second', 'third'].map((text) => ({ role: 'user', parts: [{ kind: 'text', text }] }))
  const snapshot = Projection.projectionSnapshot(Projection.semanticProjection(messages))
  const expected = Projection.renderSemantic(Projection.semanticProjection(messages.slice(0, 2)))
  assert.equal(Projection.cutoffDigest(sha256, snapshot, 2), sha256(expected))
  assert.notEqual(Projection.cutoffDigest(sha256, snapshot, 2), Projection.cutoffDigest(sha256, snapshot, 3))
})

test('WHAT[provider-projection-011] the Host crypto adapter agrees with the reference hash and transport fields stay out of the semantic projection', async () => {
  // The production adapter is HostDigest.sha256Hex (the single Host crypto
  // adapter); composition injects it into the journal (DelegationJournalAdapter).
  // It must agree byte-for-byte with the reference implementation the injected
  // tests used, so the boundary tests were not proving a different digest.
  const hostSha256Hex = Projection.hostSha256Hex
  assert.equal(hostSha256Hex('boundary-agreement'), sha256('boundary-agreement'))

  // Transport-only fields never reach the semantic projection, so they cannot
  // influence the canonical digest (WHAT 011: 排除时间戳、耗时、成本等传输字段).
  const plain = { role: 'assistant', parts: [{ kind: 'text', text: 'payload' }] }
  const transported = {
    ...plain,
    timestamp: '2026-01-01T00:00:00Z',
    durationMs: 1234,
    cost: 0.5,
    requestId: 'wire-transport-id',
  }
  const digestOf = (messages) =>
    Projection.cutoffDigest(hostSha256Hex, Projection.projectionSnapshot(Projection.semanticProjection(messages)), 1)
  assert.equal(digestOf([plain]), digestOf([transported]))
  const semantic = Projection.semanticProjection([transported])
  for (const field of ['timestamp', 'durationMs', 'cost', 'requestId']) {
    assert.equal(JSON.stringify(semantic).includes(field), false, `${field} must be excluded`)
  }
})


let fixtureOrdinal = 0
const productionDigest = async (callId, path) => {
  fixtureOrdinal += 1
  const sessionID = `ses-production-prefix-digest-${fixtureOrdinal}`
  let committed
  let expectedPrefix
  const record = async (hooks, directory, host, journalRuntime) => {
    const runtime = { ...journalRuntime, ...host }
    const physical = 'msg-prefix-current'
    const profile = await acceptAuthorityRoot(runtime, sessionID, 'manager', physical)
    const output = {
      message: { id: physical, sessionID, role: 'user', agent: 'manager', time: { created: 3 } },
      parts: [{ type: 'text', text: 'continue current work' }],
    }
    await hooks['chat.message']({ sessionID, messageID: physical, agent: 'manager' }, output)
    const messages = [
      { info: { id: 'msg-prefix-opening', sessionID, role: 'user', time: { created: 1 } }, parts: [{ type: 'text', text: 'opening assignment' }] },
      { info: { id: 'msg-prefix-history', sessionID, role: 'assistant', parentID: 'msg-prefix-opening', agent: 'manager', finish: 'tool-calls', time: { created: 2, completed: 3 } }, parts: [{ type: 'tool-call', tool: 'read', callID: callId, args: { path } }] },
      { info: { id: 'msg-prefix-result', sessionID, role: 'tool', time: { created: 3 } }, parts: [{ type: 'tool-result', callID: callId, output: 'read result' }] },
      { info: output.message, parts: output.parts },
    ]
    for (const message of messages) runtime.pushHostMessage(sessionID, structuredClone(message))
    runtime.pushHostMessage(sessionID, {
      info: { id: 'msg-prefix-run', sessionID, parentID: physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 4 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: structuredClone(messages) })
    const blogger = Journal.JournalSurface_snapshot(runtime.journal).sessionProjections[sessionID].companion.bloggerSessionId
    await awaitPrompted(blogger)
    const bloggerPhysical = runtime.messages.find(message => message.id?.startsWith(`msg-${blogger}-`))
    await hooks['chat.message'](
      { sessionID: blogger, messageID: bloggerPhysical.id, agent: 'blogger' },
      { message: bloggerPhysical, parts: bloggerPhysical.parts },
    )
    const checkpoint = {
      info: { id: 'msg-prefix-run', sessionID, parentID: physical, role: 'assistant', agent: 'manager', finish: 'tool-calls', time: { created: 4, completed: 5 } },
      parts: [{ type: 'tool-call', tool: 'todowrite', callID: 'call-prefix-checkpoint', args: { todos: [] } }],
    }
    const currentRun = runtime.messages.find(message => message.info?.id === checkpoint.info.id)
    Object.assign(currentRun, structuredClone(checkpoint))
    messages.push(checkpoint)
    messages.push({
      info: { id: 'msg-prefix-checkpoint-result', sessionID, role: 'tool', time: { created: 5 } },
      parts: [{ type: 'tool-result', callID: 'call-prefix-checkpoint', output: 'Todos updated' }],
    })
    runtime.pushHostMessage(sessionID, structuredClone(messages.at(-1)))
    runtime.pushHostMessage(sessionID, {
      info: { id: 'msg-prefix-next-run', sessionID, parentID: physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 6 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: structuredClone(messages) })
    const entry = {
      charge: 'Record the completed read.', occurrence: 'The source was read.',
      settlement: 'The read is complete.', consequence: 'Later work retains the result.',
      tip: Enforcer.fieldNames()[0],
    }
    const recorded = await hooks.tool.chronicle.execute(entry, { sessionID: blogger, agent: 'blogger', messageID: 'msg-blog-prefix-run', callID: 'call-blog-prefix' })
    const bloggerMessages = [
      { info: { ...bloggerPhysical, sessionID: blogger }, parts: bloggerPhysical.parts },
      { info: { id: 'msg-blog-prefix-run', sessionID: blogger, parentID: bloggerPhysical.id, role: 'assistant', agent: 'blogger', finish: 'tool-calls', time: { created: 2, completed: 3 } }, parts: [{ type: 'tool', tool: 'chronicle', callID: 'call-blog-prefix', state: { status: 'completed', input: entry, output: recorded } }] },
    ]
    for (const message of bloggerMessages) runtime.pushHostMessage(blogger, message)
    runtime.pushHostMessage(blogger, {
      info: { id: 'msg-blog-prefix-next-run', sessionID: blogger, parentID: bloggerPhysical.id, role: 'assistant', agent: 'blogger', providerID: 'provider', modelID: 'blogger-model', time: { created: 4 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID: blogger }, { messages: structuredClone(bloggerMessages) })
    assert.ok(Journal.JournalSurface_snapshot(runtime.journal).sessionProjections[sessionID].blog, JSON.stringify(Journal.JournalSurface_snapshot(runtime.journal)))
    const observation = journalFacts(join(directory, '.git')).map(fact => payloadFor(fact, 'BlogObservationCommitted')).filter(Boolean)
    assert.equal(observation.length, 1, JSON.stringify(journalFacts(join(directory, '.git'))))
    assert.equal(observation[0].NextCoverableTurnCutoffExclusive, 4)
    expectedPrefix = Projection.semanticProjection([
      { role: 'user', parts: [{ kind: 'text', text: 'opening assignment' }] },
      { role: 'assistant', parts: [{ kind: 'tool-call', callId, name: 'read', args: JSON.stringify({ path }) }] },
      { role: 'tool', parts: [{ kind: 'tool-result', callId, result: 'read result' }] },
      { role: 'user', parts: [{ kind: 'text', text: 'continue current work' }] },
    ])
    assert.equal(observation[0].NextCoveredPrefixDigest, sha256(Projection.renderSemantic(expectedPrefix)))
    const checkpointInput = { tool: 'todowrite', sessionID, callID: 'call-prefix-checkpoint' }
    const checkpointOutput = { args: { todos: [] } }
    await hooks['tool.execute.before'](checkpointInput, checkpointOutput)
    await hooks['tool.execute.after']({ ...checkpointInput, args: checkpointOutput.args }, { title: 'todowrite', output: 'Todos updated', metadata: {} })
    await hooks.event({ event: { type: 'message.part.updated', properties: { sessionID, part: { type: 'tool', tool: 'todowrite', callID: checkpointInput.callID, state: { status: 'completed' } } } } })
    assert.equal(Journal.JournalSurface_snapshot(runtime.journal).todoCheckpoints[0].checkpoints[0].callId, checkpointInput.callID)
    const nextPhysical = 'msg-prefix-next-user'
    const continuation = await Dispatch.sendContinuation({
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async () => Dispatch.admittedWithReceipt('accepted-prefix-continuation'),
    }, runtime.journal, sessionID, 'next request outside the covered prefix', 'ManagedDelegationAssignment', profile, 'Detached')
    assert.equal(continuation.ok, true, JSON.stringify(continuation))
    const nextUser = {
      message: { id: nextPhysical, sessionID, role: 'user', agent: 'manager', time: { created: 7 }, metadata: { wanxiangshu_prompt_key: continuation.key } },
      parts: [{ type: 'text', text: 'next request outside the covered prefix', metadata: { wanxiangshu_prompt_key: continuation.key } }],
    }
    await hooks['chat.message']({ sessionID, messageID: nextPhysical, agent: 'manager' }, nextUser)
    const nextMessage = { info: nextUser.message, parts: nextUser.parts }
    messages.push(nextMessage)
    runtime.pushHostMessage(sessionID, structuredClone(nextMessage))
    const solvingRun = {
      info: { id: 'msg-prefix-solving-run', sessionID, parentID: nextPhysical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 8 } },
      parts: [],
    }
    runtime.pushHostMessage(sessionID, solvingRun)
    const tentative = { messages: structuredClone(messages) }
    await hooks['experimental.chat.messages.transform']({ sessionID }, tentative)
    assert.ok(tentative.messages.some(message => message.info?.source === 'companion-memory'), 'production Wire must admit the proven prefix')
    solvingRun.info.finish = 'tool-calls'
    solvingRun.info.time.completed = 9
    solvingRun.parts = [{ type: 'tool-call', tool: 'read', callID: 'call-prefix-tail', args: { path: 'tail' } }]
    messages.push(structuredClone(solvingRun), {
      info: { id: 'msg-prefix-tail-result', sessionID, role: 'tool', time: { created: 9 } },
      parts: [{ type: 'tool-result', callID: 'call-prefix-tail', output: 'tail result' }],
    })
    runtime.pushHostMessage(sessionID, structuredClone(messages.at(-1)))
    runtime.pushHostMessage(sessionID, {
      info: { id: 'msg-prefix-continuing-run', sessionID, parentID: nextPhysical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 10 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: structuredClone(messages) })
    const rebases = journalFacts(join(directory, '.git')).map(fact => payloadFor(fact, 'PrefixRebaseCommitted')).filter(Boolean)
    assert.equal(rebases.length, 1)
    assert.equal(rebases[0].CutoffExclusive, 4)
    assert.equal(rebases[0].CoveredPrefixDigest, sha256(Projection.renderSemantic(expectedPrefix)))
    assert.notEqual(rebases[0].CoveredPrefixDigest, sha256(Projection.renderSemantic({ ...expectedPrefix, messages: expectedPrefix.messages.slice(0, 3) })))
    committed = rebases[0]
  }
  await withRestartablePlugin(async (start, directory, host) => {
    const hooks = await start()
    await configureManagedPlugin(hooks)
    await host.withRuntime(runtime => record(hooks, directory, host, runtime))
    await host.stop(hooks)
    const reopened = await start()
    await configureManagedPlugin(reopened)
    await host.withRuntime(async (runtime) => {
      const state = Journal.JournalSurface_snapshot(runtime.journal).sessionProjections[sessionID]
      assert.deepEqual(state.prefixEpoch, { epochId: 1n, snapshotPresent: true })
      const projection = await Trace.currentProjection(runtime.journal, sessionID)
      assert.equal(sha256(Projection.renderSemantic({ ...projection, messages: projection.messages.slice(0, committed.CutoffExclusive) })), committed.CoveredPrefixDigest)
      const rebases = journalFacts(join(directory, '.git')).map(fact => payloadFor(fact, 'PrefixRebaseCommitted')).filter(Boolean)
      assert.deepEqual(rebases, [committed])
    })
  })
  return committed.CoveredPrefixDigest
}

test('WHAT[provider-projection-011] registered production transforms persist and replay the exact semantic prefix digest', async () => {
  const first = await productionDigest('physical-read-1', 'x')
  const renamed = await productionDigest('physical-read-2', 'x')
  assert.equal(first, renamed)
})

test('WHAT[provider-projection-011] real argument changes alter the durable production digest', async () => {
  const first = await productionDigest('physical-read-1', 'x')
  const changed = await productionDigest('physical-read-1', 'y')
  assert.notEqual(first, changed)
})

for (const mutation of ['hash', 'cutoff']) {
  test(`WHAT[provider-projection-011] the registered production proof detects an incorrect ${mutation} wiring`, async () => {
    const run = promisify(execFile)
    const env = { ...process.env, WANXIANGSHU_PREFIX_DIGEST_MUTATION: mutation }
    delete env.NODE_TEST_CONTEXT
    await assert.rejects(
      () => run(process.execPath, [
        '--experimental-loader', join(import.meta.dirname, 'support/prefix-digest-mutation-loader.mjs'),
        '--test', '--test-name-pattern', 'registered production transforms persist and replay', import.meta.filename,
      ], { env }),
      error => {
        assert.equal(error.code, 1)
        assert.match(error.stdout, /production Wire must admit the proven prefix/)
        return true
      },
    )
  })
}
