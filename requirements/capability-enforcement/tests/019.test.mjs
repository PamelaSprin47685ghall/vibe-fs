import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const SESSION = 'ses-process-capability'

const accepted = { accepted: true, failure: null }

const rejected = (failure) => ({ accepted: false, failure })

const assertResult = (actual, expected) => {
  assertJsData(actual, 'quiescence result')
  assert.deepEqual(actual, expected)
}

const freshPermit = (gate, session = SESSION) => {
  assertOpaque(gate, 'quiescence gate')
  quiescence.beginAttempt(gate, session)
  const permit = quiescence.observeIdle(gate, session)
  assertOpaque(permit, 'quiescence permit')
  return permit
}

test('WHAT[capability-enforcement-019] a different gate requires its own current-attempt admission', () => {
  const priorGate = quiescence.create()
  const priorPermit = freshPermit(priorGate)
  const currentGate = quiescence.create()

  assertResult(quiescence.tryConsume(currentGate, priorPermit), rejected('WrongOwner'))

  const unownedIdle = quiescence.observeIdle(currentGate, SESSION)
  assertOpaque(unownedIdle, 'unowned idle permit')
  assertResult(quiescence.tryConsume(currentGate, unownedIdle), rejected('NoFreshIdle'))

  const currentPermit = freshPermit(currentGate)
  assertResult(quiescence.tryConsume(currentGate, currentPermit), accepted)
})

test.todo('WHAT[capability-enforcement-019] actual serialization boundaries and process restart must reject restored process authority; two gates in one process do not prove non-durability')

test('WHAT[capability-enforcement-019] live opaque permit resources stay bounded to the current session attempt', () => {
  const gate = quiescence.create()
  assert.equal(quiescence.livePermitCount(gate), 0)

  for (let attempt = 0; attempt < 256; attempt += 1) {
    quiescence.beginAttempt(gate, SESSION)
    const first = quiescence.observeIdle(gate, SESSION)
    const replay = quiescence.observeIdle(gate, SESSION)
    assertOpaque(first, 'current idle permit')
    assertOpaque(replay, 'replayed current idle permit')
    assert.equal(quiescence.livePermitCount(gate), 1, 'idle replay and attempt churn must not grow the resource registry')
  }

  quiescence.revoke(gate, SESSION)
  assert.equal(quiescence.livePermitCount(gate), 0, 'revocation must release current-attempt permit resources')

  freshPermit(gate)
  quiescence.dropSession(gate, SESSION)
  assert.equal(quiescence.livePermitCount(gate), 0, 'session cleanup must release permit resources')
})
