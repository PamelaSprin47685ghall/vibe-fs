import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

test('WHAT[capability-enforcement-013] a capability-shaped object cannot replace an issued opaque permit', () => {
  const gate = quiescence.create()
  const fake = { tag: 0, fields: ['session', 1], name: 'QuiescencePermit' }
  assert.deepEqual(quiescence.tryConsume(gate, fake), { accepted: false, failure: 'WrongOwner' })
  assert.deepEqual(quiescence.tryRelease(gate, fake), { accepted: false, failure: 'WrongOwner' })
  quiescence.beginAttempt(gate, 'session')
  const permit = quiescence.observeIdle(gate, 'session')
  assert.deepEqual(quiescence.tryConsume(gate, permit), { accepted: true, failure: null })
})

test.todo('WHAT[capability-enforcement-013] six causal categories and positive vocabulary classification require wider boundary evidence; one unforgeable permit does not prove the full classification')
