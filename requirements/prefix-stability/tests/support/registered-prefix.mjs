import assert from 'node:assert/strict'
import fs, { readFileSync, readdirSync, realpathSync } from 'node:fs'
import { syncBuiltinESMExports } from 'node:module'
import { join, sep } from 'node:path'
import * as Journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as Enforcer from '../../../../dist/Enforcer/Surface.js'
import * as Dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as Recovery from '../../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'
import * as Trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'
import * as WorkRecord from '../../../../dist/Mission/WorkRecord/Surface.js'
import { acceptAuthorityRoot, awaitPrompted } from '../../../verification-system/tests/support/plugin-fixture.mjs'

const payload = (value, name) => {
  if (Array.isArray(value) && value[0] === name) return value[1]
  if (value !== null && typeof value === 'object') {
    for (const nested of Object.values(value)) {
      const found = payload(nested, name)
      if (found !== undefined) return found
    }
  }
}

const records = directory => readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
  const path = join(directory, entry.name)
  if (entry.isDirectory()) return records(path)
  if (!entry.name.endsWith('.ndjson')) return []
  return readFileSync(path, 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line))
})

export const facts = (directory, name) => records(join(directory, '.Herdchestra')).map(value => payload(value, name)).filter(Boolean)
export const state = (runtime, sessionID) => Journal.JournalSurface_snapshot(runtime.journal).sessionProjections[sessionID]
export const prefixHistory = directory => ({
  rebases: facts(directory, 'PrefixRebaseCommitted'),
  reanchors: facts(directory, 'ContextReanchored'),
  observations: facts(directory, 'BlogObservationCommitted'),
})

export const checkpointEvidence = (runtime, sessionID, callId) =>
  WorkRecord.journalCheckpointEvidence(runtime.journal, sessionID, callId)

export const candidateEvidence = (runtime, sessionID, phaseCutoff = null) =>
  WorkRecord.journalCandidateEvidence(runtime.journal, sessionID, phaseCutoff)

export const commitOfferedFrame = async (runtime, sessionID, label) => {
  const request = await WorkRecord.nextBloggerRequest(runtime.journal, sessionID)
  assert.ok(request)
  const payload = await Journal.JournalSurface_writePayload(runtime.journal, 'Recorded the material offered in ' + label)
  assert.equal(payload.ok, true)
  const proof = {
    bloggerSessionId: request.bloggerSessionId, requestId: request.requestId,
    frameEpoch: request.frameEpoch,
    previousIngestedThroughSequence: request.previousSequence,
    nextIngestedThroughSequence: request.nextSequence,
    previousCoverableTurnCutoffExclusive: request.previousCutoff,
    nextCoverableTurnCutoffExclusive: request.nextCutoff,
    nextCoveredPrefixDigest: request.nextCoveredPrefixDigest,
    textRef: payload.blobRef, textDigest: payload.blobDigest, toolCallIds: [], tipRuleId: label,
    fieldNameAtCommit: label, observedPrefixEpoch: request.observedPrefixEpoch,
  }
  const committed = await WorkRecord.appendBlogObservation(runtime.journal, sessionID, `run-${label}`, proof)
  assert.equal(committed.ok, true, JSON.stringify(committed.error))
  return proof
}

export const observeFact = async (directory, name, action) => {
  const observed = Promise.withResolvers()
  const names = Array.isArray(name) ? name : [name]
  const before = new Map(names.map(key => [key, facts(directory, key).length]))
  const observeCommitted = () => {
    for (const key of names) {
      const committed = facts(directory, key)
      if (committed.length > before.get(key)) observed.resolve(committed.at(-1))
    }
  }
  const append = fs.appendFileSync
  const eventRoot = realpathSync(join(directory, '.Herdchestra', 'wanxiangshu', 'events')) + sep
  fs.appendFileSync = (path, content, ...options) => {
    append(path, content, ...options)
    if (typeof path === 'string' && path.startsWith(eventRoot) && names.some(key => String(content).includes(`"${key}"`))) {
      observeCommitted()
    }
  }
  syncBuiltinESMExports()
  try {
    await action()
    observeCommitted()
    return await observed.promise
  } finally {
    fs.appendFileSync = append
    syncBuiltinESMExports()
  }
}
export const prefixState = (runtime, sessionID) => {
  const current = state(runtime, sessionID)
  return structuredClone({ prefixEpoch: current.prefixEpoch, blog: current.blog, authorityRoot: current.promptAuthority?.activeLogicalRun })
}

export const checkedTest = async (context, name, action) => {
  let failure
  await context.test(name, async child => {
    try {
      await action(child)
    } catch (error) {
      failure = error
      throw error
    }
  })
  if (failure) throw failure
}

export const coveredSession = async (hooks, runtime, directory, sessionID, testContext) => {
  const physical = 'msg-covered-root'
  let profile
  const root = { message: { id: physical, sessionID, role: 'user', agent: 'manager', time: { created: 3 } }, parts: [{ type: 'text', text: 'continue current work' }] }
  const messages = [
    { info: { id: 'msg-opening', sessionID, role: 'user', time: { created: 1 } }, parts: [{ type: 'text', text: 'opening assignment' }] },
    { info: { id: 'msg-read', sessionID, role: 'assistant', parentID: 'msg-opening', agent: 'manager', finish: 'tool-calls', time: { created: 2, completed: 3 } }, parts: [{ type: 'tool-call', tool: 'read', callID: 'call-read', args: { path: 'x' } }] },
    { info: { id: 'msg-read-result', sessionID, role: 'tool', time: { created: 3 } }, parts: [{ type: 'tool-result', callID: 'call-read', output: 'read result' }] },
    { info: root.message, parts: root.parts },
  ]
  const push = message => runtime.pushHostMessage(sessionID, message)
  messages.forEach(message => push(structuredClone(message)))
  const checkpoint = { info: { id: 'msg-checkpoint', sessionID, parentID: physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 4 } }, parts: [] }
  push(checkpoint)
  const transform = async (activeHooks = hooks) => {
    const output = { messages: structuredClone(messages) }
    await activeHooks['experimental.chat.messages.transform']({ sessionID }, output)
    return output
  }
  let blogger
  let bloggerPhysical
  let bloggerMessages
  let bloggerNext
  const check = (name, action) => testContext ? checkedTest(testContext, name, action) : action()
  await check('registered authority and Host history create the real Blogger request', async () => {
    profile = await acceptAuthorityRoot(runtime, sessionID, 'manager', physical)
    await hooks['chat.message']({ sessionID, messageID: physical, agent: 'manager' }, root)
    await transform()
    blogger = state(runtime, sessionID).companion.bloggerSessionId
    await awaitPrompted(blogger)
    bloggerPhysical = runtime.messages.find(message => message.id?.startsWith(`msg-${blogger}-`))
    assert.ok(bloggerPhysical)
    await hooks['chat.message']({ sessionID: blogger, messageID: bloggerPhysical.id, agent: 'blogger' }, { message: bloggerPhysical, parts: bloggerPhysical.parts })
  })
  const running = { info: { id: 'msg-covered-next-run', sessionID, parentID: physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: 6 } }, parts: [] }
  await check('actual Chronicle execution and Host transcript commit the covered frame', async () => {
    Object.assign(checkpoint.info, { finish: 'tool-calls', time: { created: 4, completed: 5 } })
    checkpoint.parts = [{ type: 'tool-call', tool: 'todowrite-for-manager', callID: 'call-checkpoint', args: { program: 'class Js extends JsProgram { async run() { return {}; } }', view: { prefixUntil: 1 } } }]
    messages.push(structuredClone(checkpoint), { info: { id: 'msg-checkpoint-result', sessionID, role: 'tool', time: { created: 5 } }, parts: [{ type: 'tool-result', callID: 'call-checkpoint', output: 'Todos updated' }] })
    push(structuredClone(messages.at(-1)))
    push(running)
    await transform()
    const entry = { charge: 'Record the completed read.', occurrence: 'The source was read.', settlement: 'The read is complete.', consequence: 'Later work retains the result.', tipName: Enforcer.fieldNames()[0], opinion: 'The durable frame preserves the outcome for later context.', advice: 'Carry the recorded material into the next step.', }
    const recorded = await hooks.tool.chronicle.execute(entry, { sessionID: blogger, agent: 'blogger', messageID: 'msg-blog-run', callID: 'call-blog' })
    bloggerMessages = [
      { info: { ...bloggerPhysical, sessionID: blogger }, parts: bloggerPhysical.parts },
      { info: { id: 'msg-blog-run', sessionID: blogger, parentID: bloggerPhysical.id, role: 'assistant', agent: 'blogger', finish: 'tool-calls', time: { created: 2, completed: 3 } }, parts: [{ type: 'tool', tool: 'chronicle', callID: 'call-blog', state: { status: 'completed', input: entry, output: recorded } }] },
    ]
    bloggerMessages.forEach(message => runtime.pushHostMessage(blogger, message))
    bloggerNext = { info: { id: 'msg-blog-next', sessionID: blogger, parentID: bloggerPhysical.id, role: 'assistant', agent: 'blogger', providerID: 'provider', modelID: 'blogger-model', time: { created: 4 } }, parts: [] }
    runtime.pushHostMessage(blogger, bloggerNext)
    await hooks['experimental.chat.messages.transform']({ sessionID: blogger }, { messages: structuredClone(bloggerMessages) })
    assert.equal(facts(directory, 'BlogObservationCommitted').length, 1)
    assert.equal(facts(directory, 'BlogObservationCommitted')[0].NextCoverableTurnCutoffExclusive, 4)
  })
  await check('native checkpoint tool completion opens the real phase window', async () => {
    const callID = 'call-checkpoint'
    await hooks.event({
      event: {
        type: 'message.part.updated',
        properties: {
          sessionID,
          part: {
            type: 'tool',
            tool: 'todowrite-for-manager',
            callID,
            state: {
              status: 'completed',
              input: { program: 'class Js extends JsProgram { async run() { return {}; } }', view: { prefixUntil: 1 } },
            },
          },
        },
      },
    })
    assert.equal(Journal.JournalSurface_snapshot(runtime.journal).viewCheckpoints[0].checkpoints[0].callId, callID)
    assert.deepEqual(checkpointEvidence(runtime, sessionID, callID), { checkpointTurn: 4, desiredCutoff: 4 })
    assert.equal(facts(directory, 'BlogObservationCommitted')[0].NextCoverableTurnCutoffExclusive, 4)
    assert.deepEqual(await candidateEvidence(runtime, sessionID, 2), { ok: false, reason: 'NoCoverage' }, 'CTX-029: a frame crossing the desired boundary must not advance')
    assert.deepEqual(await candidateEvidence(runtime, sessionID, 4), { ok: true, cutoff: 4 })
  })
  return { sessionID, profile, messages, transform, push, clock: 10, blogger, bloggerPhysical, bloggerMessages, bloggerNext }
}

export const commitPendingBlogger = async (hooks, runtime, directory, session) => {
  const request = facts(directory, 'BloggerRequestMaterialized').at(-1)
  const previous = facts(directory, 'BlogObservationCommitted').at(-1)
  assert.equal(request.PreviousIngestedThroughSequence, previous.NextIngestedThroughSequence)
  assert.equal(request.NextIngestedThroughSequence, '6')
  assert.deepEqual(Trace.semanticCursorAfter(Trace.cursor(6), Trace.snapshot(runtime.journal, session.sessionID)), { turn: 6, partIndex: 0 })
  const entry = { charge: 'Record the completed checkpoint.', occurrence: 'The checkpoint completed.', settlement: 'The checkpoint is durable.', consequence: 'Later work retains the checkpoint.', tipName: Enforcer.fieldNames()[0], opinion: 'The durable frame preserves the outcome for later context.', advice: 'Carry the recorded material into the next step.', }
  const recorded = await hooks.tool.chronicle.execute(entry, { sessionID: session.blogger, agent: 'blogger', messageID: session.bloggerNext.info.id, callID: 'call-blog-second' })
  session.bloggerNext.info.finish = 'tool-calls'
  session.bloggerNext.info.time.completed = 5
  session.bloggerNext.parts = [{ type: 'tool', tool: 'chronicle', callID: 'call-blog-second', state: { status: 'completed', input: entry, output: recorded } }]
  session.bloggerMessages.push(structuredClone(session.bloggerNext))
  runtime.pushHostMessage(session.blogger, { info: { id: 'msg-blog-final', sessionID: session.blogger, parentID: session.bloggerPhysical.id, role: 'assistant', agent: 'blogger', providerID: 'provider', modelID: 'blogger-model', time: { created: 6 } }, parts: [] })
  await hooks['experimental.chat.messages.transform']({ sessionID: session.blogger }, { messages: structuredClone(session.bloggerMessages) })
  const committed = facts(directory, 'BlogObservationCommitted')
  assert.equal(committed.length, 2)
  assert.deepEqual(committed.at(-1).RequestId, request.RequestId)
  assert.deepEqual(committed.at(-1).ProviderRun, ['ProviderRunIdentity', session.bloggerNext.info.id])
  assert.equal(committed.at(-1).PreviousIngestedThroughSequence, request.PreviousIngestedThroughSequence)
  assert.equal(committed.at(-1).NextIngestedThroughSequence, request.NextIngestedThroughSequence)
  assert.equal(committed.at(-1).NextCoverableTurnCutoffExclusive, 6)
  const cycles = state(runtime, session.sessionID).bloggerCycles
  assert.equal(cycles.receiptCount, 2)
  assert.ok(cycles.receiptRuns.includes(session.bloggerNext.info.id), 'the second producer must have its durable cycle receipt')
  return committed.at(-1)
}

export const continuation = async (hooks, runtime, session, physical, origin = 'ManagedDelegationAssignment') => {
  const sent = await Dispatch.sendContinuation({ SubscribeTerminal: () => ({ Dispose() {} }), SendPrompt: async () => Dispatch.admittedWithReceipt(`receipt-${physical}`) }, runtime.journal, session.sessionID, `continue ${physical}`, origin, session.profile, 'Detached')
  assert.equal(sent.ok, true, JSON.stringify(sent))
  session.clock += 10
  const metadata = sent.observation.metadata
  const output = { message: { id: physical, sessionID: session.sessionID, role: 'user', agent: 'manager', time: { created: session.clock }, metadata }, parts: [{ type: 'text', text: `continue ${physical}`, metadata }] }
  await hooks['chat.message']({ sessionID: session.sessionID, messageID: physical, agent: 'manager' }, output)
  const message = { info: output.message, parts: output.parts }
  session.messages.push(message)
  session.push(structuredClone(message))
  const run = { info: { id: `run-${physical}`, sessionID: session.sessionID, parentID: physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: session.clock + 1 } }, parts: [] }
  session.push(run)
  await hooks['chat.params']({ sessionID: session.sessionID, message: output.message, agent: 'manager', model: { providerID: 'provider', id: 'manager-model', capabilities: {} } }, {})
  const projected = await session.transform(hooks)
  return { physical, message: output.message, run, projected }
}

export const completeToolAttempt = async (session, attempt, hooks) => {
  attempt.run.info.finish = 'tool-calls'
  attempt.run.info.time.completed = session.clock + 2
  attempt.run.parts = [{ type: 'tool-call', tool: 'read', callID: `call-${attempt.physical}`, args: { path: 'tail' } }]
  session.messages.push(structuredClone(attempt.run), { info: { id: `result-${attempt.physical}`, sessionID: session.sessionID, role: 'tool', time: { created: session.clock + 2 } }, parts: [{ type: 'tool-result', callID: `call-${attempt.physical}`, output: 'tail result' }] })
  session.push(structuredClone(session.messages.at(-1)))
  session.push({ info: { id: `next-${attempt.physical}`, sessionID: session.sessionID, parentID: attempt.physical, role: 'assistant', agent: 'manager', providerID: 'provider', modelID: 'manager-model', time: { created: session.clock + 3 } }, parts: [] })
  return session.transform(hooks)
}

export const failAttempt = async (hooks, runtime, session, attempt, outcome) => {
  const error = { name: outcome === 'Aborted' ? 'MessageAbortedError' : 'APIError', data: { message: 'controlled provider failure', statusCode: 400, isRetryable: false } }
  attempt.run.info.error = error
  attempt.run.info.time.completed = session.clock + 2
  await hooks.event({ event: { type: 'message.updated', properties: { info: attempt.run.info } } })
  return Recovery.journalExecutionStatus(runtime.journal, session.sessionID, attempt.physical)
}
