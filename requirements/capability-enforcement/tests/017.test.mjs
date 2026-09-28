import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

test('WHAT[capability-enforcement-017] released one-shot opportunity cannot be consumed again', {
  todo: '10-D1: current QuiescencePermit release reopens the same permit; resolve one-shot versus reusable reservation contract before changing runtime semantics',
}, () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, 'multiplicity-conflict')
  const permit = quiescence.observeIdle(gate, 'multiplicity-conflict')
  assert.equal(quiescence.tryConsume(gate, permit).accepted, true)
  assert.equal(quiescence.tryRelease(gate, permit).accepted, true)
  assert.equal(quiescence.tryConsume(gate, permit).accepted, false)
})
