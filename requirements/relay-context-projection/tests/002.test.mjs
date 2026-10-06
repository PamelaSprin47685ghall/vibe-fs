import assert from 'node:assert/strict'
import test from 'node:test'
import {withSuccessor, textMessage} from './support/cut.mjs'

test('WHAT[relay-context-projection-002] real cut rejects an old request but accepts the owner-issued successor identity', async () => {
  await withSuccessor(async ({session, history, gate, apply}) => {
    const historySnapshot = structuredClone(history)
    assert.deepEqual(await apply(structuredClone(historySnapshot)), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
    const successor = [...history, gate]
    assert.deepEqual(await apply(successor), {disposition: 'current-iteration', messages: successor, interrupted: []})
    const falseGate = [...structuredClone(historySnapshot), gate, textMessage('unadmitted-wake', 'user', 'runtime/manager-assess suicide new authority')]
    assert.deepEqual(await apply(falseGate), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})

    // An admitted retry continuation after the cut must be accepted as the current iteration
    // rather than falsely classified as a stale retired attempt and interrupted.
    const retryContinuation = textMessage('retry-continuation-1', 'user', 'provider retry prompt')
    const withAdmittedRetry = [...structuredClone(historySnapshot), gate, retryContinuation]
    assert.deepEqual(await apply(withAdmittedRetry, true), {
      disposition: 'current-iteration', messages: withAdmittedRetry, interrupted: []
    })
  })
})
