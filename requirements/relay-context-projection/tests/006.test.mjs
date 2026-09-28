import assert from 'node:assert/strict'
import test from 'node:test'
import {withSuccessor} from './support/cut.mjs'

test('WHAT[relay-context-projection-006] actual projection of the same committed facts is deterministic and adds no messages', async () => {
  await withSuccessor(async ({history, gate, apply}) => {
    const input = [...history, gate]
    const before = structuredClone(input)
    assert.deepEqual(await apply(input), await apply(input))
    assert.deepEqual((await apply(input)).messages, before)
    assert.deepEqual(input, before)
  })
})
