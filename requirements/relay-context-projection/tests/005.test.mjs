import assert from 'node:assert/strict'
import test from 'node:test'
import {withSuccessor} from './support/cut.mjs'

test('WHAT[relay-context-projection-005] actual Continue dispatch stays in the same physical session and preserves the prior history', async () => {
  await withSuccessor(async ({session, runtime, history, gate, apply}) => {
    assert.equal(runtime.prompts[0].path.id, session)
    const input = [...history, gate]
    assert.deepEqual((await apply(input)).messages, input)
  })
})
