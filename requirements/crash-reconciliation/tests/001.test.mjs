import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const accepted = { accepted: true, failure: null }
const rejected = failure => ({ accepted: false, failure })

test('WHAT[crash-reconciliation-001] independently created gates cannot share process-local permits', () => {
  const before = quiescence.create()
  quiescence.beginAttempt(before, 'session')
  const permit = quiescence.observeIdle(before, 'session')
  const after = quiescence.create()
  assert.deepEqual(quiescence.tryConsume(after, permit), rejected('WrongOwner'))
  assert.deepEqual(quiescence.tryConsume(before, permit), accepted)
})

test('WHAT[crash-reconciliation-001] idle without a current attempt cannot mint send authority', () => {
  const gate = quiescence.create()
  assert.deepEqual(quiescence.tryConsume(gate, quiescence.observeIdle(gate, 'session')), rejected('NoFreshIdle'))
  quiescence.beginAttempt(gate, 'session')
  assert.deepEqual(quiescence.tryConsume(gate, quiescence.observeIdle(gate, 'session')), accepted)
})

test.todo('WHAT[crash-reconciliation-001] real process restart drops all guard permit waiter and detector state without automatic side effects (GAP-149)')
