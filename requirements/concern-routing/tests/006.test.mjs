import assert from 'node:assert/strict'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

test('WHAT[CONCERN-ROUTING-006] retirement prevents old messages crossing into a same-concern replacement generation', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.publish('sender', 'msg-old', 'build', 'old message', state).state
  state = concern.retire('owner-a', 'build', 'gen-1', state).state
  const rebound = concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state)
  assert.equal(rebound.ok, true)
  assert.equal(rebound.appended, true)
  state = rebound.state
  assert.deepEqual(concern.prepare('owner-b', state).messages, [])
})
