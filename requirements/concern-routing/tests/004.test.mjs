import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[concern-routing-004] messages wait for the next Pair Hint and coverage commits only on placement', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.publish('sender', 'msg-1', 'build', 'failure found', state).state

  const preparedA = concern.prepare('owner-a', state)
  const preparedB = concern.prepare('owner-a', state)
  assert.deepEqual(preparedA, preparedB, 'uncommitted placement must be byte-stable and non-consuming')
  assert.deepEqual(preparedA.messages, [{ id: 'build', message: 'failure found' }])

  state = concern.place('owner-a', preparedA.announcedGenerations, preparedA.deliveredMessages, state).state
  assert.deepEqual(concern.prepare('owner-a', state).messages, [])
})
