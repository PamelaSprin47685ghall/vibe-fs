import assert from 'node:assert/strict'
import test from 'node:test'
import {withReview, scores} from '../../relay-assessment/tests/support/plugin.mjs'
import {withSuccessor, messageId} from '../../relay-context-projection/tests/support/cut.mjs'

test('WHAT[relay-retirement-008] actual Accepted suicide returns without abort and the next transform stops the retired attempt', async () => {
  await withReview(async ({execute, hooks, runtime, session}) => {
    assert.match(await execute(scores('PERFECT')), /recorded = true/)
    const result = await hooks.tool.suicide.execute({}, {sessionID: session, callID: 'suicide-call', messageID: 'retirement-run', agent: 'manager'})
    assert.match(result, /finished = true/)
    assert.deepEqual(runtime.abortedIds, [])
    const output = {messages: [
      {info: {id: 'user-root', role: 'user', sessionID: session, model: {providerID: 'provider', modelID: 'manager-model'}}, parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}]},
      {info: {id: 'retirement-run', role: 'assistant'}, parts: [{type: 'text', text: 'Old closing tail'}]},
    ]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    assert.deepEqual(output.messages, [])
    assert.deepEqual(runtime.abortedIds, [session])
    assert.equal(runtime.prompts.length, 0)
  })
})

test('WHAT[relay-retirement-008] actual Continue starts one same-session successor whose transform retains the old physical history without another abort', async () => {
  await withSuccessor(async ({hooks, runtime, session, history, gate}) => {
    const output = {messages: [...structuredClone(history), gate]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    for (const prior of history) {
      assert.ok(output.messages.some(message => messageId(message) === messageId(prior)), `lost physical message ${messageId(prior)}`)
    }
    assert.ok(output.messages.some(message => messageId(message) === messageId(gate)))
    assert.deepEqual(runtime.abortedIds, [session])
    assert.equal(runtime.prompts.length, 1)
  })
})

test.todo('WHAT[relay-retirement-008] a controlled in-flight Host interrupt prevents successor dispatch until exact provider-step release and physical interruption complete')
