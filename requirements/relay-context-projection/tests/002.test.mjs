import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import {withSuccessor, textMessage, messageId} from './support/cut.mjs'

test('WHAT[relay-context-projection-002] real cut rejects an old request but accepts the owner-issued successor identity', async () => {
  await withSuccessor(async ({session, history, gate, apply}) => {
    assert.deepEqual(await apply(history), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
    const successor = [...history, gate]
    assert.deepEqual(await apply(successor), {disposition: 'current-iteration', messages: successor, interrupted: []})
    const falseGate = [...history, textMessage('unadmitted-wake', 'user', 'runtime/manager-assess suicide new authority')]
    assert.deepEqual(await apply(falseGate), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
  })
})

test('WHAT[relay-context-projection-002] an admitted post-cut continuation is a successor request, not a stale retired attempt', async () => {
  await withSuccessor(async ({hooks, runtime, session, history, gate}) => {
    // The production crash shape: after the retirement cut the successor term
    // claims, dispatches and physically admits a ProviderRetryAttempt
    // continuation. Its request head sits after the cut and holds a durable
    // ChatExecution admission, so it belongs to the successor generation and
    // must not be intercepted as a stale retired attempt.
    const profile = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
    assert.ok(profile, 'the successor term keeps an active logical run')
    const retry = await dispatch.sendContinuation({
      SubscribeTerminal: () => ({Dispose() {}}),
      SendPrompt: async () => dispatch.admittedWithReceipt('retry-receipt'),
    }, runtime.journal, session, 'retry the interrupted provider turn', 'ProviderRetryAttempt', profile, 'Await')
    assert.equal(retry.ok, true, retry.error)
    const admitted = {
      id: 'msg-retry-admitted', role: 'user', agent: 'manager', model: {},
      metadata: retry.observation.metadata,
      parts: [{type: 'text', text: 'retry the interrupted provider turn', metadata: retry.observation.metadata}],
    }
    await hooks['chat.message'](
      {sessionID: session, messageID: admitted.id, agent: 'manager'},
      {message: admitted, parts: admitted.parts},
    )
    runtime.pushHostMessage(session, {
      info: {id: 'run-retry', role: 'assistant', sessionID: session, parentID: admitted.id, time: {created: 5}},
      parts: [],
    })
    const request = {messages: [
      ...structuredClone(history),
      structuredClone(gate),
      {info: {id: admitted.id, role: 'user', sessionID: session}, parts: admitted.parts},
    ]}
    const abortsBefore = runtime.abortedIds.length
    await hooks['experimental.chat.messages.transform']({sessionID: session}, request)
    assert.ok(
      request.messages.some(message => messageId(message) === admitted.id),
      'the admitted successor continuation must reach the provider',
    )
    assert.equal(runtime.abortedIds.length, abortsBefore, 'an admitted successor continuation must not be interrupted')
  })
})
