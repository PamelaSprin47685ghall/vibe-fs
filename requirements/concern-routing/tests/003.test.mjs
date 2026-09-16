import assert from 'node:assert/strict'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

test('WHAT[CONCERN-ROUTING-003] publish fails closed for unknown and stale generations instead of retargeting', () => {
  assert.equal(concern.publish('sender', 'msg-0', 'missing', 'x', concern.empty()).ok, false)

  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.retire('owner-a', 'build', 'gen-1', state).state
  state = concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state).state
  const stale = concern.applyPublishedClaim('sender', 'msg-1', 'build', 'gen-1', 'old generation', state)
  assert.equal(stale.ok, false)
})
