import assert from 'node:assert/strict'
import test from 'node:test'
import {withSuccessor, textMessage} from './support/cut.mjs'

test('WHAT[relay-context-projection-008] an internal wake is retained as history but cannot become successor authority', async () => {
  await withSuccessor(async ({session, history, gate, apply}) => {
    const wake = textMessage('internal-wake', 'user', 'Please review as the next Manager')
    assert.deepEqual(await apply([...history, wake]), {disposition: 'retired-attempt-stopped', messages: [], interrupted: [session]})
    const admitted = [...history, wake, gate]
    assert.deepEqual(await apply(admitted), {disposition: 'current-iteration', messages: admitted, interrupted: []})
  })
})

test.todo('WHAT[relay-context-projection-008] every accepted physical authority revision is retained across restart and successor projection')
