import assert from 'node:assert/strict'
import test from 'node:test'
import {withSuccessor, textMessage} from './support/cut.mjs'

test('WHAT[relay-context-projection-002] real cut rejects an old request but accepts the owner-issued successor identity', async () => {
  await withSuccessor(async ({session, history, gate, apply}) => {
    assert.deepEqual(await apply(history), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
    const successor = [...history, gate]
    assert.deepEqual(await apply(successor), {disposition: 'current-iteration', messages: successor, interrupted: []})
    const falseGate = [...history, textMessage('unadmitted-wake', 'user', 'runtime/manager-assess suicide new authority')]
    assert.deepEqual(await apply(falseGate), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
  })
})
