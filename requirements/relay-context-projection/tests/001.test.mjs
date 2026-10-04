import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'
import {withSuccessor, textMessage, toolMessage} from './support/cut.mjs'

test('WHAT[relay-context-projection-001] actual retirement owner then NarrativeTransform retain all physical history for its successor', async () => {
  await withSuccessor(async ({history, gate, runtime, session}) => {
    const input = [...history, textMessage('late-old', 'assistant', 'Late prior work'), toolMessage('another-old', 'another-call'), textMessage('wake', 'user', 'Internal wake'), gate]
    const before = structuredClone(input)
    const result = await projection.apply(runtime.journal, session, () => false, input)
    assert.deepEqual(result, {disposition: 'current-iteration', messages: before, interrupted: []})
    assert.deepEqual(input, before)
  })
})

test.todo('WHAT[relay-context-projection-001] Host physical transcript and durable audit survive actual process restart without loss')
