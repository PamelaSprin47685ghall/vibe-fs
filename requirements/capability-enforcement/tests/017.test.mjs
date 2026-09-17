import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

test('WHAT[ENF-017] authority multiplicity enforces one-shot atomicity and prevents duplicated consumption', () => {
  const gate = quiescence.create()
  const sessionId = 'ses-enf-017'

  // 1. One-shot consumption: first tryConsume succeeds, repeated tryConsume is rejected with AlreadyConsumed
  quiescence.beginAttempt(gate, sessionId)
  const permit1 = quiescence.observeIdle(gate, sessionId)

  const firstConsume = quiescence.tryConsume(gate, permit1)
  assert.equal(firstConsume.accepted, true)
  assert.equal(firstConsume.failure, undefined)

  const secondConsume = quiescence.tryConsume(gate, permit1)
  assert.equal(secondConsume.accepted, false)
  assert.equal(secondConsume.failure, 'AlreadyConsumed')

  const thirdConsume = quiescence.tryConsume(gate, permit1)
  assert.equal(thirdConsume.accepted, false)
  assert.equal(thirdConsume.failure, 'AlreadyConsumed')

  // 2. Release atomicity: tryRelease permanently closes the one-shot opportunity
  quiescence.beginAttempt(gate, sessionId)
  const permit2 = quiescence.observeIdle(gate, sessionId)

  const releaseResult = quiescence.tryRelease(gate, permit2)
  assert.equal(releaseResult.accepted, true)

  const consumeAfterRelease = quiescence.tryConsume(gate, permit2)
  assert.equal(consumeAfterRelease.accepted, false)
  assert.equal(consumeAfterRelease.failure, 'AlreadyConsumed')
})
